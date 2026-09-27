//! Reject recipe regeneration that strands persistent promoted origins.
use forge_project::zone_authoring::{ZoneAuthoringHead, ZoneBase, session::ZoneRequest};
mod recipe;
pub use recipe::ReconciliationRecipe;

/// Display ordering only: proximity never creates an origin correspondence.
pub struct RankedTree<'a> {
    pub tree: &'a crate::VegetationInstance,
    /// Horizontal distance to the authored placement, absent for a tombstone.
    pub distance_m: Option<f64>,
}

/// Ephemeral candidate inventory, not a persisted authoring document.
pub struct ReconciliationChoices {
    pub recipe: ReconciliationRecipe,
    pub source_hash: forge_core::ContentHash,
    pub base: ZoneBase,
    pub origins: Vec<forge_project::zone_authoring::promotion::PromotedInstance>,
    pub generated: std::sync::Arc<crate::GeneratedVegetation>,
    pub generation_hash: forge_core::ContentHash,
}

impl ReconciliationChoices {
    pub fn for_seed(head: &ZoneAuthoringHead, seed: u64) -> Result<Self, String> {
        Self::for_recipe(head, ReconciliationRecipe::Seed { seed })
    }

    pub fn for_recipe(
        head: &ZoneAuthoringHead,
        recipe: ReconciliationRecipe,
    ) -> Result<Self, String> {
        let mut candidate = head.clone();
        candidate.base = recipe.base(head)?;
        let origins = head
            .corrections
            .promotions
            .clone()
            .filter(|v| !v.is_empty())
            .ok_or("Aucune origine promue à réconcilier")?;
        let review = head.review_regeneration(candidate.base.clone())?;
        if let Some(reason) = review.refusal_message() {
            return Err(reason);
        }
        let terrain = crate::river_projection::project(&candidate)?.terrain;
        let (_, vegetation) = candidate.effective()?;
        let plan = crate::VegetationGenerationPlan::from_documents_with_buildings(
            &terrain,
            &vegetation.ok_or("Végétation absente")?,
            head.corrections.paths.as_deref().unwrap_or_default(),
            head.corrections.rivers.as_deref().unwrap_or_default(),
            head.corrections.buildings.as_deref().unwrap_or_default(),
        )
        .map_err(|e| e.to_string())?;
        Ok(Self {
            recipe,
            source_hash: head.content_hash()?,
            base: candidate.base,
            origins,
            generation_hash: plan.source_hash(),
            generated: plan.generate().map_err(|e| e.to_string())?,
        })
    }

    pub fn trees(&self) -> impl Iterator<Item = &crate::VegetationInstance> {
        self.generated
            .chunks
            .iter()
            .flat_map(|c| &c.instances)
            .filter(|i| i.kind == forge_project::VegetationKind::Tree)
    }

    pub fn ranked_trees(
        &self,
        head: &ZoneAuthoringHead,
        placement_id: forge_core::EntityId,
    ) -> Result<Vec<RankedTree<'_>>, String> {
        if head.content_hash()? != self.source_hash {
            return Err("Correspondances périmées : réexaminer la recette".into());
        }
        if !self.origins.iter().any(|o| o.placement_id == placement_id) {
            return Err("Objet promu absent de cette réconciliation".into());
        }
        let position = head
            .corrections
            .placements
            .as_deref()
            .unwrap_or_default()
            .iter()
            .find(|p| p.id == placement_id)
            .map(|p| p.transform.translation_m);
        let mut trees = self
            .trees()
            .map(|tree| RankedTree {
                tree,
                distance_m: position.map(|p| {
                    (f64::from(tree.position_m.x) - f64::from(p.x))
                        .hypot(f64::from(tree.position_m.z) - f64::from(p.z))
                }),
            })
            .collect::<Vec<_>>();
        trees.sort_by(|a, b| {
            a.distance_m
                .unwrap_or(0.0)
                .total_cmp(&b.distance_m.unwrap_or(0.0))
                .then_with(|| a.tree.id.as_entity_id().cmp(&b.tree.id.as_entity_id()))
        });
        Ok(trees)
    }

    pub fn prepare(
        &self,
        head: &ZoneAuthoringHead,
        selections: &std::collections::BTreeMap<forge_core::EntityId, forge_core::EntityId>,
    ) -> Result<ZoneRequest, String> {
        if head.content_hash()? != self.source_hash {
            return Err("Correspondances périmées : réexaminer la recette".into());
        }
        if selections.len() != self.origins.len() {
            return Err("Choisir explicitement une origine pour chaque objet".into());
        }
        let trees = self
            .trees()
            .map(|t| (t.id.as_entity_id(), t))
            .collect::<std::collections::BTreeMap<_, _>>();
        let mut origins = self.origins.clone();
        for origin in &mut origins {
            let selected = selections
                .get(&origin.placement_id)
                .ok_or("Correspondance absente")?;
            let tree = trees.get(selected).ok_or("Arbre proposé absent")?;
            origin.instance_id = *selected;
            origin.layer_id = tree.layer_id.clone();
            origin.generation_hash = self.generation_hash;
        }
        origins.sort_by(|a, b| (&a.layer_id, a.instance_id).cmp(&(&b.layer_id, b.instance_id)));
        let proposal = forge_project::zone_authoring::promotion::PromotionReconciliation {
            base: self.base.clone(),
            origins,
        };
        let hash = forge_core::canonical_json_sha256(&(self.source_hash, &proposal))
            .map_err(|e| e.to_string())?;
        prepare_reconciliation(
            head,
            proposal,
            forge_core::CommandId::deterministic(format!("reconcile:{hash}")),
        )
    }
}

/// Explicit author-selected replacements; no nearest-tree guess and no placement mutation.
pub fn prepare_reconciliation(
    head: &ZoneAuthoringHead,
    proposal: forge_project::zone_authoring::promotion::PromotionReconciliation,
    command_id: forge_core::CommandId,
) -> Result<ZoneRequest, String> {
    let request = head.prepare_promotion_reconciliation(proposal.clone(), command_id)?;
    let mut candidate = head.clone();
    candidate.base = proposal.base;
    candidate.corrections.promotions = Some(proposal.origins);
    validate_origins(&candidate)?;
    Ok(request)
}

pub fn prepare_regeneration(
    head: &ZoneAuthoringHead,
    base: ZoneBase,
    command_id: forge_core::CommandId,
) -> Result<ZoneRequest, String> {
    let request = head.prepare_regeneration(base.clone(), command_id)?;
    if head
        .corrections
        .promotions
        .as_ref()
        .is_none_or(Vec::is_empty)
    {
        return Ok(request);
    }
    let mut candidate = head.clone();
    candidate.base = base;
    validate_origins(&candidate)?;
    Ok(request)
}

/// No mutation and no fallback: every persistent origin must still resolve.
pub fn validate_origins(head: &ZoneAuthoringHead) -> Result<(), String> {
    head.validate()?;
    let origins = head.corrections.promotions.as_deref().unwrap_or_default();
    if origins.is_empty() {
        return Ok(());
    }
    let terrain = crate::river_projection::project(head)?.terrain;
    let (_, vegetation) = head.effective()?;
    let vegetation = vegetation.ok_or("Promoted origins require vegetation")?;
    crate::VegetationGenerationPlan::from_documents_with_buildings(
        &terrain,
        &vegetation,
        head.corrections.paths.as_deref().unwrap_or_default(),
        head.corrections.rivers.as_deref().unwrap_or_default(),
        head.corrections.buildings.as_deref().unwrap_or_default(),
    )
    .and_then(|plan| plan.with_promotions(origins))
    .and_then(|plan| plan.generate())
    .map_err(|error| describe_conflict(head, error))?;
    Ok(())
}

fn describe_conflict(head: &ZoneAuthoringHead, error: crate::VegetationGenerationError) -> String {
    let crate::VegetationGenerationError::UnresolvedPromotions(missing) = error else {
        return format!("Régénération refusée : {error}. Aucune retouche supprimée.");
    };
    let origins = head.corrections.promotions.as_deref().unwrap_or_default();
    let placements = head.corrections.placements.as_deref().unwrap_or_default();
    let details = missing
        .iter()
        .take(8)
        .map(|(layer, instance)| {
            let placement = origins
                .iter()
                .find(|o| &o.layer_id == layer && o.instance_id == *instance)
                .and_then(|o| placements.iter().find(|p| p.id == o.placement_id));
            let name =
                placement.map_or("objet supprimé, exclusion conservée", |p| p.name.as_str());
            format!("{name} · couche {layer} · origine {instance}")
        })
        .collect::<Vec<_>>()
        .join(" ; ");
    format!(
        "Régénération refusée : {} origine(s) promue(s) introuvable(s). {details}. Aucune retouche supprimée. Conserver la recette actuelle ou résoudre explicitement ces origines avant de réessayer.",
        missing.len()
    )
}
