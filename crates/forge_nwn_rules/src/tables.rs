//! Bounded reader for NWN 2DA tables.
//!
//! Tables are provided by the local NWN installation or extracted read-only by
//! the caller. No 2DA data is embedded or redistributed by this crate.

use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;
use std::fmt;
use std::path::Path;

pub const MAX_2DA_BYTES: usize = 16 * 1024 * 1024;
pub const MAX_2DA_ROWS: usize = 100_000;

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct TwoDaRow {
    pub id: u32,
    pub values: Vec<String>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct TwoDaTable {
    pub columns: Vec<String>,
    pub rows: Vec<TwoDaRow>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum TwoDaError {
    TooLarge(usize),
    Header,
    Columns,
    Row { line: usize, detail: String },
    TooManyRows(usize),
    MissingFile(String),
    Io(String),
    Empty,
}

impl fmt::Display for TwoDaError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::TooLarge(size) => write!(formatter, "2DA exceeds the byte limit ({size})"),
            Self::Header => formatter.write_str("2DA header is absent or unsupported"),
            Self::Columns => formatter.write_str("2DA column line is absent"),
            Self::Row { line, detail } => write!(formatter, "2DA row {line}: {detail}"),
            Self::TooManyRows(count) => write!(formatter, "2DA row limit exceeded ({count})"),
            Self::MissingFile(path) => write!(formatter, "2DA file is absent: {path}"),
            Self::Io(detail) => write!(formatter, "2DA I/O failed: {detail}"),
            Self::Empty => formatter.write_str("2DA table is empty"),
        }
    }
}

impl std::error::Error for TwoDaError {}

impl TwoDaTable {
    pub fn parse(text: &str) -> Result<Self, TwoDaError> {
        if text.len() > MAX_2DA_BYTES {
            return Err(TwoDaError::TooLarge(text.len()));
        }
        let mut lines = text
            .trim_start_matches('\u{feff}')
            .lines()
            .map(str::trim)
            .filter(|line| !line.is_empty());
        let header = lines.next().ok_or(TwoDaError::Header)?;
        if !header.eq_ignore_ascii_case("2DA V2.0") {
            return Err(TwoDaError::Header);
        }
        let columns = lines
            .next()
            .ok_or(TwoDaError::Columns)?
            .split_whitespace()
            .map(str::to_owned)
            .collect::<Vec<_>>();
        if columns.is_empty() {
            return Err(TwoDaError::Columns);
        }
        let mut rows = Vec::new();
        for (index, line) in lines.enumerate() {
            if rows.len() >= MAX_2DA_ROWS {
                return Err(TwoDaError::TooManyRows(rows.len()));
            }
            let mut parts = line.split_whitespace();
            let Some(id) = parts.next() else {
                continue;
            };
            let id = id.parse::<u32>().map_err(|_| TwoDaError::Row {
                line: index + 3,
                detail: format!("invalid row id {id}"),
            })?;
            let values = parts.map(str::to_owned).collect::<Vec<_>>();
            if values.len() > columns.len() {
                return Err(TwoDaError::Row {
                    line: index + 3,
                    detail: format!("{} values for {} columns", values.len(), columns.len()),
                });
            }
            rows.push(TwoDaRow { id, values });
        }
        if rows.is_empty() {
            return Err(TwoDaError::Empty);
        }
        Ok(Self { columns, rows })
    }

    pub fn load(path: &Path) -> Result<Self, TwoDaError> {
        let bytes = std::fs::read(path).map_err(|error| {
            if error.kind() == std::io::ErrorKind::NotFound {
                TwoDaError::MissingFile(path.display().to_string())
            } else {
                TwoDaError::Io(error.to_string())
            }
        })?;
        if bytes.len() > MAX_2DA_BYTES {
            return Err(TwoDaError::TooLarge(bytes.len()));
        }
        let text = String::from_utf8_lossy(&bytes);
        Self::parse(&text)
    }

    #[must_use]
    pub fn column_index(&self, name: &str) -> Option<usize> {
        self.columns
            .iter()
            .position(|column| column.eq_ignore_ascii_case(name))
    }

    #[must_use]
    pub fn value<'a>(&'a self, row: &'a TwoDaRow, column: &str) -> Option<&'a str> {
        let index = self.column_index(column)?;
        row.values.get(index).map(String::as_str)
    }

    #[must_use]
    pub fn row(&self, id: u32) -> Option<&TwoDaRow> {
        self.rows.iter().find(|row| row.id == id)
    }

    #[must_use]
    pub fn find_label(&self, label: &str) -> Option<&TwoDaRow> {
        let index = self.column_index("Label")?;
        self.rows
            .iter()
            .find(|row| row.values.get(index).is_some_and(|value| value == label))
    }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct TwoDaAudit {
    pub schema: String,
    pub directory: String,
    pub tables: BTreeMap<String, TwoDaTable>,
    pub missing: Vec<String>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct TwoDaTableSummary {
    pub rows: usize,
    pub columns: usize,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct TwoDaSummary {
    pub schema: String,
    pub tables: BTreeMap<String, TwoDaTableSummary>,
    pub missing: Vec<String>,
}

impl TwoDaAudit {
    pub fn load(directory: &Path) -> Result<Self, TwoDaError> {
        if !directory.is_dir() {
            return Err(TwoDaError::MissingFile(directory.display().to_string()));
        }
        let mut tables = BTreeMap::new();
        let mut missing = Vec::new();
        for name in ["classes", "spells", "feat", "racialtypes"] {
            let path = directory.join(format!("{name}.2da"));
            match TwoDaTable::load(&path) {
                Ok(table) => {
                    tables.insert(name.to_owned(), table);
                }
                Err(TwoDaError::MissingFile(_)) => missing.push(name.to_owned()),
                Err(error) => return Err(error),
            }
        }
        Ok(Self {
            schema: "forge.nwn-2da-audit@1".to_owned(),
            directory: directory.display().to_string(),
            tables,
            missing,
        })
    }

    #[must_use]
    pub fn summary(&self) -> TwoDaSummary {
        TwoDaSummary {
            schema: "forge.nwn-2da-summary@1".to_owned(),
            tables: self
                .tables
                .iter()
                .map(|(name, table)| {
                    (
                        name.clone(),
                        TwoDaTableSummary {
                            rows: table.rows.len(),
                            columns: table.columns.len(),
                        },
                    )
                })
                .collect(),
            missing: self.missing.clone(),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    const SAMPLE: &str = "2DA V2.0\n\n     Label      HitDie  PlayerClass\n0    Barbarian  12      1\n1    Bard       6       1\n";

    #[test]
    fn parses_columns_and_rows() {
        let table = TwoDaTable::parse(SAMPLE).expect("2da");
        assert_eq!(table.columns.len(), 3);
        assert_eq!(table.rows.len(), 2);
        assert_eq!(table.value(&table.rows[0], "Label"), Some("Barbarian"));
        assert_eq!(
            table.row(1).and_then(|row| table.value(row, "HitDie")),
            Some("6")
        );
        assert!(table.find_label("Bard").is_some());
    }

    #[test]
    fn rejects_invalid_headers_and_rows() {
        assert_eq!(TwoDaTable::parse("not 2da"), Err(TwoDaError::Header));
        assert!(matches!(
            TwoDaTable::parse("2DA V2.0\nLabel\nx Bad\n"),
            Err(TwoDaError::Row { .. })
        ));
    }

    #[test]
    fn audit_reports_missing_tables() {
        let directory = std::env::temp_dir().join(format!("forge-2da-{}", std::process::id()));
        std::fs::create_dir_all(&directory).expect("dir");
        std::fs::write(directory.join("classes.2da"), SAMPLE).expect("write");
        let audit = TwoDaAudit::load(&directory).expect("audit");
        assert!(audit.tables.contains_key("classes"));
        assert!(audit.missing.contains(&"spells".to_owned()));
        std::fs::remove_dir_all(directory).expect("cleanup");
    }
}
