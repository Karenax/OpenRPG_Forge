// Qualification policy, not application logic. Every reported condition is required.
export function layoutVerdict(result) {
  if (result.dualScreen) {
    const mapOnlyPassed = result.mapOnly === true && result.panelsHidden === true
      && result.viewportShare >= 0.90 && result.canvas?.height > 350;
    const panelsPassed = result.mapOnly === false && result.panelsHidden === false
      && result.scene?.width >= 150 && result.topicWindow?.width >= 250
      && result.canvas?.width > 600 && result.canvas?.height > 350
      && result.canvasClearOfPanel === true;
    const chromePassed = result.tabCount === 8 && result.active.length === 1
      && result.dockHidden === true && result.mapModeAction === true
      && result.horizontalOverflow === false && (mapOnlyPassed || panelsPassed);
    return {
      chromePassed,
    layoutPassed: chromePassed && result.consolePresent === true
        && result.compassPresent === true && result.axisHudPresent === true && result.axisHudClearOfLibrary === true
        && Array.isArray(result.primaryActions)
        && result.primaryActions.every(action => action.fullyVisible === true)
        && Array.isArray(result.inspectorFields)
        && result.inspectorFields.every(field => field.fullyVisible === true),
    };
  }
  const chromePassed = result.tabCount === 8 && result.active.length === 1
    && result.navigationVisible && !result.horizontalOverflow && result.canvasClearOfPanel === true
    && result.canvas?.width > 600 && result.canvas?.height > 350;
  return {
    chromePassed,
    layoutPassed: chromePassed && result.panelFits === true && result.axisHudPresent === true && result.axisHudClearOfLibrary === true
      && (result.libraryOpen === false || (result.libraryOpen === true
        && result.library?.scrollHeight <= result.library?.clientHeight))
      && Array.isArray(result.primaryActions)
      && result.primaryActions.every(action => action.fullyVisible === true)
      && Array.isArray(result.inspectorFields)
      && result.inspectorFields.every(field => field.fullyVisible === true),
  };
}

export function secondaryVerdict(result) {
  const panes = result.panes;
  const chromePassed = result.surface === 'secondary'
    && Array.isArray(result.topicSelection) && result.topicSelection.length === 8
    && result.topicSelection.filter(t => t.pressed === 'true').length === 1
    && result.topicSelection.every(t => ['true','false'].includes(t.pressed))
    && Array.isArray(panes) && panes.length === 3
    && panes.every(p => p.visible === true && p.width >= 200 && p.height >= 350)
    && panes.slice(1).every((p,i) => p.x >= panes[i].right)
    && result.horizontalOverflow === false && result.mapModeAction === true;
  return {chromePassed,layoutPassed:chromePassed && result.inspectorFits === true
    && Array.isArray(result.primaryActions) && result.primaryActions.length >= 9
    && result.primaryActions.every(action => action.fullyVisible === true)
    && Array.isArray(result.inspectorFields)
    && result.inspectorFields.every(field => field.fullyVisible === true)};
}
