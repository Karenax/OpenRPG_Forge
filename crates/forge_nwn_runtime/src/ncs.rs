//! Bounded parser for the NWN `NCS V1.0` bytecode container.
//!
//! Multi-byte operands are big-endian. The parser validates the header, file size,
//! instruction framing and every supported operand before the VM sees a byte.

use serde::{Deserialize, Serialize};
use std::fmt;

pub const NCS_SIGNATURE: &[u8; 8] = b"NCS V1.0";
pub const NCS_HEADER_BYTES: usize = 13;
pub const MAX_NCS_BYTES: usize = 4 * 1024 * 1024;
pub const MAX_NCS_INSTRUCTIONS: usize = 200_000;

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum NcsError {
    TooLarge(usize),
    InvalidSignature,
    UnsupportedVersion(u8),
    SizeMismatch { declared: u32, actual: usize },
    Truncated { offset: usize },
    UnknownOpcode { offset: usize, opcode: u8 },
    InvalidConstantType { offset: usize, type_code: u8 },
    TooManyInstructions(usize),
    InvalidString { offset: usize, length: usize },
}

impl fmt::Display for NcsError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::TooLarge(size) => write!(formatter, "NCS exceeds the byte limit ({size})"),
            Self::InvalidSignature => formatter.write_str("NCS signature is absent"),
            Self::UnsupportedVersion(version) => {
                write!(formatter, "unsupported NCS version byte {version:#04x}")
            }
            Self::SizeMismatch { declared, actual } => {
                write!(
                    formatter,
                    "NCS declared size {declared} differs from {actual} bytes"
                )
            }
            Self::Truncated { offset } => {
                write!(formatter, "truncated NCS instruction at {offset}")
            }
            Self::UnknownOpcode { offset, opcode } => {
                write!(formatter, "unknown NCS opcode {opcode:#04x} at {offset}")
            }
            Self::InvalidConstantType { offset, type_code } => {
                write!(
                    formatter,
                    "invalid constant type {type_code:#04x} at {offset}"
                )
            }
            Self::TooManyInstructions(count) => {
                write!(formatter, "too many NCS instructions ({count})")
            }
            Self::InvalidString { offset, length } => {
                write!(
                    formatter,
                    "invalid NCS string at {offset} with length {length}"
                )
            }
        }
    }
}

impl std::error::Error for NcsError {}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case")]
pub enum NcsInstruction {
    CopyDownSp { offset: i32, size: u16 },
    Reserve { type_code: u8 },
    CopyTopSp { offset: i32, size: u16 },
    ConstInt(i32),
    ConstFloat(f32),
    ConstString(String),
    ConstObject(u32),
    Action { routine: u16, arguments: u8 },
    LogAnd,
    Equal,
    MoveSp(i32),
    Jump(i32),
    JumpSubroutine(i32),
    JumpIfZero(i32),
    Return,
    Unsupported { opcode: u8, type_code: u8 },
}

#[derive(Debug, Clone, PartialEq)]
pub struct NcsInstructionRecord {
    pub offset: usize,
    pub instruction: NcsInstruction,
}

#[derive(Debug, Clone, PartialEq)]
pub struct NcsProgram {
    pub resref: String,
    pub declared_size: u32,
    pub instructions: Vec<NcsInstructionRecord>,
    pub bytes: Vec<u8>,
}

impl NcsProgram {
    pub fn parse(resref: impl Into<String>, bytes: &[u8]) -> Result<Self, NcsError> {
        let declared_size = validate_header(bytes)?;
        let instructions = decode_instructions(bytes)?;
        Ok(Self {
            resref: resref.into(),
            declared_size,
            instructions,
            bytes: bytes.to_vec(),
        })
    }

    #[must_use]
    pub fn instruction_at(&self, offset: usize) -> Option<&NcsInstructionRecord> {
        self.instructions
            .iter()
            .find(|record| record.offset == offset)
    }

    pub fn unsupported(&self) -> impl Iterator<Item = &NcsInstructionRecord> {
        self.instructions
            .iter()
            .filter(|record| matches!(record.instruction, NcsInstruction::Unsupported { .. }))
    }
}

fn validate_header(bytes: &[u8]) -> Result<u32, NcsError> {
    if bytes.len() > MAX_NCS_BYTES {
        return Err(NcsError::TooLarge(bytes.len()));
    }
    if bytes.len() < NCS_HEADER_BYTES || &bytes[..8] != NCS_SIGNATURE {
        return Err(NcsError::InvalidSignature);
    }
    if bytes[8] != 0x42 {
        return Err(NcsError::UnsupportedVersion(bytes[8]));
    }
    let declared_size = u32::from_be_bytes([bytes[9], bytes[10], bytes[11], bytes[12]]);
    if declared_size as usize != bytes.len() {
        return Err(NcsError::SizeMismatch {
            declared: declared_size,
            actual: bytes.len(),
        });
    }
    Ok(declared_size)
}

fn decode_instructions(bytes: &[u8]) -> Result<Vec<NcsInstructionRecord>, NcsError> {
    let mut instructions = Vec::new();
    let mut offset = NCS_HEADER_BYTES;
    while offset < bytes.len() {
        if instructions.len() >= MAX_NCS_INSTRUCTIONS {
            return Err(NcsError::TooManyInstructions(instructions.len()));
        }
        let start = offset;
        let (instruction, length) = decode(bytes, offset)?;
        if length == 0 {
            return Err(NcsError::Truncated { offset: start });
        }
        instructions.push(NcsInstructionRecord {
            offset: start,
            instruction,
        });
        offset = offset.saturating_add(length);
    }
    if offset != bytes.len() {
        return Err(NcsError::Truncated { offset });
    }
    Ok(instructions)
}

#[allow(clippy::too_many_lines)]
fn decode(bytes: &[u8], offset: usize) -> Result<(NcsInstruction, usize), NcsError> {
    let opcode = *bytes.get(offset).ok_or(NcsError::Truncated { offset })?;
    let type_code = *bytes
        .get(offset + 1)
        .ok_or(NcsError::Truncated { offset })?;
    let need = |count: usize| -> Result<(), NcsError> {
        (offset + count <= bytes.len())
            .then_some(())
            .ok_or(NcsError::Truncated { offset })
    };
    match opcode {
        0x01 => {
            need(8)?;
            let delta = i32::from_be_bytes([
                bytes[offset + 2],
                bytes[offset + 3],
                bytes[offset + 4],
                bytes[offset + 5],
            ]);
            let size = u16::from_be_bytes([bytes[offset + 6], bytes[offset + 7]]);
            Ok((
                NcsInstruction::CopyDownSp {
                    offset: delta,
                    size,
                },
                8,
            ))
        }
        0x02 => Ok((NcsInstruction::Reserve { type_code }, 2)),
        0x03 => {
            need(8)?;
            let delta = i32::from_be_bytes([
                bytes[offset + 2],
                bytes[offset + 3],
                bytes[offset + 4],
                bytes[offset + 5],
            ]);
            let size = u16::from_be_bytes([bytes[offset + 6], bytes[offset + 7]]);
            Ok((
                NcsInstruction::CopyTopSp {
                    offset: delta,
                    size,
                },
                8,
            ))
        }
        0x04 => decode_constant(bytes, offset, type_code),
        0x05 => {
            need(5)?;
            let routine = u16::from_be_bytes([bytes[offset + 2], bytes[offset + 3]]);
            let arguments = bytes[offset + 4];
            Ok((NcsInstruction::Action { routine, arguments }, 5))
        }
        0x06 => {
            need(2)?;
            Ok((NcsInstruction::LogAnd, 2))
        }
        0x0B => {
            need(2)?;
            Ok((NcsInstruction::Equal, 2))
        }
        0x0C | 0x0D..=0x1A | 0x1C | 0x22 | 0x2A | 0x2B | 0x2D => {
            need(2)?;
            Ok((NcsInstruction::Unsupported { opcode, type_code }, 2))
        }
        0x23 | 0x24 | 0x25 | 0x28 | 0x29 => {
            need(6)?;
            Ok((NcsInstruction::Unsupported { opcode, type_code }, 6))
        }
        0x1B => {
            need(6)?;
            let delta = i32::from_be_bytes([
                bytes[offset + 2],
                bytes[offset + 3],
                bytes[offset + 4],
                bytes[offset + 5],
            ]);
            Ok((NcsInstruction::MoveSp(delta), 6))
        }
        0x1D => {
            need(6)?;
            let delta = read_i32(bytes, offset + 2);
            Ok((NcsInstruction::Jump(delta), 6))
        }
        0x1E => {
            need(6)?;
            let delta = read_i32(bytes, offset + 2);
            Ok((NcsInstruction::JumpSubroutine(delta), 6))
        }
        0x1F => {
            need(6)?;
            let delta = read_i32(bytes, offset + 2);
            Ok((NcsInstruction::JumpIfZero(delta), 6))
        }
        0x20 => {
            need(2)?;
            Ok((NcsInstruction::Return, 2))
        }
        0x21 | 0x26 | 0x27 => {
            need(8)?;
            Ok((NcsInstruction::Unsupported { opcode, type_code }, 8))
        }
        0x2C => {
            need(10)?;
            Ok((NcsInstruction::Unsupported { opcode, type_code }, 10))
        }
        _ => Err(NcsError::UnknownOpcode { offset, opcode }),
    }
}

fn decode_constant(
    bytes: &[u8],
    offset: usize,
    type_code: u8,
) -> Result<(NcsInstruction, usize), NcsError> {
    match type_code {
        0x03 => {
            if offset + 6 > bytes.len() {
                return Err(NcsError::Truncated { offset });
            }
            Ok((NcsInstruction::ConstInt(read_i32(bytes, offset + 2)), 6))
        }
        0x04 => {
            if offset + 6 > bytes.len() {
                return Err(NcsError::Truncated { offset });
            }
            let value = f32::from_bits(u32::from_be_bytes([
                bytes[offset + 2],
                bytes[offset + 3],
                bytes[offset + 4],
                bytes[offset + 5],
            ]));
            Ok((NcsInstruction::ConstFloat(value), 6))
        }
        0x05 => {
            if offset + 4 > bytes.len() {
                return Err(NcsError::Truncated { offset });
            }
            let length = u16::from_be_bytes([bytes[offset + 2], bytes[offset + 3]]) as usize;
            let end = offset + 4 + length;
            if end > bytes.len() {
                return Err(NcsError::InvalidString { offset, length });
            }
            let value = String::from_utf8_lossy(&bytes[offset + 4..end]).into_owned();
            Ok((NcsInstruction::ConstString(value), 4 + length))
        }
        0x06 => {
            if offset + 6 > bytes.len() {
                return Err(NcsError::Truncated { offset });
            }
            let value = u32::from_be_bytes([
                bytes[offset + 2],
                bytes[offset + 3],
                bytes[offset + 4],
                bytes[offset + 5],
            ]);
            Ok((NcsInstruction::ConstObject(value), 6))
        }
        _ => Err(NcsError::InvalidConstantType { offset, type_code }),
    }
}

fn read_i32(bytes: &[u8], offset: usize) -> i32 {
    i32::from_be_bytes([
        bytes[offset],
        bytes[offset + 1],
        bytes[offset + 2],
        bytes[offset + 3],
    ])
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn rejects_bad_header_and_size() {
        assert_eq!(
            NcsProgram::parse("x", b"not ncs"),
            Err(NcsError::InvalidSignature)
        );
        let mut bytes = vec![0_u8; NCS_HEADER_BYTES];
        bytes[..8].copy_from_slice(NCS_SIGNATURE);
        bytes[8] = 0x42;
        assert!(matches!(
            NcsProgram::parse("x", &bytes),
            Err(NcsError::SizeMismatch { .. })
        ));
    }

    #[test]
    fn decodes_the_fixture_prefix() {
        // Minimal `main` wrapper: JSR +8, RETN, RSADD int, CONSTI 0, RETN.
        let mut bytes = Vec::new();
        bytes.extend_from_slice(NCS_SIGNATURE);
        bytes.push(0x42);
        bytes.extend_from_slice(&[0, 0, 0, 0]);
        bytes.extend_from_slice(&[0x1E, 0x00, 0x00, 0x00, 0x00, 0x08]);
        bytes.extend_from_slice(&[0x20, 0x00]);
        bytes.extend_from_slice(&[0x02, 0x03]);
        bytes.extend_from_slice(&[0x04, 0x03, 0x00, 0x00, 0x00, 0x00]);
        bytes.extend_from_slice(&[0x20, 0x00]);
        let size = u32::try_from(bytes.len()).expect("test NCS size fits u32");
        bytes[9..13].copy_from_slice(&size.to_be_bytes());
        let program = NcsProgram::parse("test", &bytes).expect("parse");
        assert_eq!(program.instructions.len(), 5);
        assert!(matches!(
            program.instructions[0].instruction,
            NcsInstruction::JumpSubroutine(8)
        ));
        assert!(matches!(
            program.instructions[2].instruction,
            NcsInstruction::Reserve { type_code: 3 }
        ));
    }
}
