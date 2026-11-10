int StartingConditional()
{
    object oModule = GetModule();
    return GetLocalInt(oModule, "ab_has_relic") == 1
        && GetLocalInt(oModule, "ab_quest_complete") == 0;
}
