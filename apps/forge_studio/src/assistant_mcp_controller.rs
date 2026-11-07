//! Shared native controller for the Studio UI and its stdio client thread.
use crate::{
    assistant_asset_protocol::AssetSelection,
    assistant_assets_native::BoundCatalog,
    assistant_mcp::ToolCall,
    assistant_mcp_native::Inbox,
    mcp_protocol::{McpRequest, McpView, ScopeRequest},
};
use forge_project::{
    assistant::selection::{PlanningContext, SelectionScope},
    authoring::{AuthoringRequest, host::AuthoringHost},
};
use serde_json::Value;
use std::path::{Path, PathBuf};

#[derive(Default)]
pub struct Controller {
    stdio_open: bool,
    scope: Option<ScopeRequest>,
    context: Option<PlanningContext>,
    inbox: Option<Inbox>,
    calls: u64,
    message: String,
    storage_root: Option<PathBuf>,
}

impl Controller {
    /// Native configuration only; never supplied by UI or MCP arguments.
    pub fn set_storage_root(&mut self, root: &Path) -> Result<(), String> {
        let root = root.canonicalize().map_err(|e| e.to_string())?;
        if !root.is_dir()
            || self
                .storage_root
                .as_ref()
                .is_some_and(|previous| *previous != root)
            || self.inbox.is_some() && self.storage_root.is_none()
        {
            return Err("MCP staging must keep the same native authoring root".into());
        }
        self.storage_root = Some(root);
        Ok(())
    }
    #[must_use]
    pub fn authorized(&self) -> bool {
        self.stdio_open && self.inbox.is_some()
    }
    pub fn revoke_view(&mut self) -> Result<McpView, String> {
        self.revoke();
        self.message =
            "Périmètre MCP révoqué ; les propositions écartées ne peuvent plus être appliquées."
                .into();
        self.status()
    }
    pub fn set_transport_open(&mut self, open: bool) {
        self.stdio_open = open;
        if !open {
            self.revoke();
        }
        self.message = if open {
            "Transport MCP stdio ouvert ; aucune donnée autorisée."
        } else {
            "Transport MCP fermé ; autorisation révoquée."
        }
        .into();
    }
    fn revoke(&mut self) {
        self.scope = None;
        self.context = None;
        self.inbox = None;
    }
    pub fn status(&self) -> Result<McpView, String> {
        Ok(McpView {
            stdio_open: self.stdio_open,
            authorized: self.inbox.is_some(),
            scope: self.scope.clone(),
            context: self
                .context
                .as_ref()
                .map(serde_json::to_value)
                .transpose()
                .map_err(|error| error.to_string())?,
            context_hash: self
                .context
                .as_ref()
                .map(PlanningContext::content_hash)
                .transpose()?,
            proposals: self
                .inbox
                .as_ref()
                .map(Inbox::proposals)
                .transpose()?
                .unwrap_or_default(),
            tool_calls: self.calls,
            authoring: None,
            message: if self.message.is_empty() {
                "Pour connecter un client MCP, lancer ce Studio avec --mcp-stdio depuis ce client."
                    .into()
            } else {
                self.message.clone()
            },
        })
    }
    pub fn ui(&mut self, host: &mut AuthoringHost, request: McpRequest) -> Result<McpView, String> {
        self.ui_with_assets(host, request, |_| {
            Err("La sélection d’assets exige une vérification native de la bibliothèque.".into())
        })
    }

    pub fn ui_with_assets(
        &mut self,
        host: &mut AuthoringHost,
        request: McpRequest,
        mut resolve: impl FnMut(&AssetSelection) -> Result<BoundCatalog, String>,
    ) -> Result<McpView, String> {
        let mut authoring = None;
        match request {
            McpRequest::Status => {}
            McpRequest::Revoke => {
                self.revoke();
                self.message="Périmètre MCP révoqué ; les propositions écartées ne peuvent plus être appliquées.".into();
            }
            McpRequest::Preview { scope } => {
                let context = prepare(host, &scope, &mut resolve)?.context().clone();
                self.revoke();
                self.context = Some(context);
                self.scope = Some(scope);
                self.message =
                    "Aucun partage actif. Vérifier les données puis autoriser le client stdio."
                        .into();
            }
            McpRequest::Authorize {
                scope,
                expected_context_hash,
            } => {
                if !self.stdio_open {
                    return Err("Le Studio n'a pas de transport MCP stdio ouvert.".into());
                }
                let bound_scope = prepare(host, &scope, &mut resolve)?;
                let context = bound_scope.context().clone();
                if context.content_hash()? != expected_context_hash
                    || self.scope.as_ref() != Some(&scope)
                    || self.context.as_ref() != Some(&context)
                {
                    return Err("Le périmètre a changé ; préparer et relire ses données avant autorisation.".into());
                }
                // A retried confirmation on the same reviewed scope must not
                // discard staged proposals. Preview explicitly replaces a scope.
                if self.inbox.is_none() {
                    self.inbox = Some(Inbox::authorize_scope(
                        host,
                        bound_scope,
                        self.storage_root.as_deref(),
                    )?);
                }
                self.message="Client MCP autorisé sur ce contexte uniquement. Les propositions attendent une approbation Studio.".into();
            }
            McpRequest::Apply {
                command_id,
                review_hash,
            } => {
                let inbox = self
                    .inbox
                    .as_mut()
                    .ok_or("Autorisation MCP absente ou révoquée")?;
                authoring = Some(Box::new(inbox.apply_confirmed(
                    host,
                    command_id,
                    review_hash,
                )?));
                self.message = "Proposition MCP appliquée ; sauvegarder la carte.".into();
            }
        }
        let mut view = self.status()?;
        view.authoring = authoring;
        Ok(view)
    }
    pub fn context(&self, host: &mut AuthoringHost) -> Result<Option<PlanningContext>, String> {
        if !self.stdio_open || self.inbox.is_none() {
            return Ok(None);
        }
        let scope = self.scope.as_ref().ok_or("MCP scope absent")?;
        let state = host.execute(AuthoringRequest::Inspect)?;
        if state.playtest_hash.is_some()
            || state
                .zone_head
                .as_ref()
                .map(forge_project::zone_authoring::ZoneAuthoringHead::content_hash)
                .transpose()?
                != Some(scope.expected_head)
        {
            return Ok(None);
        }
        Ok(self.inbox.as_ref().map(|inbox| inbox.context().clone()))
    }
    pub fn tool(
        &mut self,
        host: &mut AuthoringHost,
        utc: &str,
        call: ToolCall,
    ) -> Result<Value, String> {
        if self.context(host)?.is_none() {
            return Err(
                "Autorisation MCP absente ou périmée ; relire le périmètre dans Studio.".into(),
            );
        }
        self.calls = self
            .calls
            .checked_add(1)
            .ok_or("MCP call counter exhausted")?;
        let inbox = self.inbox.as_mut().ok_or("MCP inbox absent")?;
        let result = crate::assistant_mcp::Backend::execute(&mut inbox.backend(host, utc), call);
        self.message = if result.is_ok() {
            "Appel MCP traité ; actualiser les propositions et relire leurs diffs."
        } else {
            "Appel MCP refusé ; aucune nouvelle application autorisée."
        }
        .into();
        result
    }
}

fn prepare(
    host: &mut AuthoringHost,
    scope: &ScopeRequest,
    resolve: &mut impl FnMut(&AssetSelection) -> Result<BoundCatalog, String>,
) -> Result<SelectionScope, String> {
    let state = host.execute(AuthoringRequest::Inspect)?;
    let head = state
        .zone_head
        .ok_or("Ouvrir une carte avant de définir le périmètre MCP.")?;
    if state.playtest_hash.is_some() || head.content_hash()? != scope.expected_head {
        return Err(
            "La carte a changé ; préparer le périmètre depuis la révision actuelle.".into(),
        );
    }
    if let Some(selection) = &scope.assets {
        let catalog = resolve(selection)?;
        SelectionScope::new_with_assets(
            &head,
            &scope.selection,
            scope.region,
            catalog.catalog_for(selection)?,
        )
    } else {
        SelectionScope::new(&head, &scope.selection, scope.region)
    }
}
