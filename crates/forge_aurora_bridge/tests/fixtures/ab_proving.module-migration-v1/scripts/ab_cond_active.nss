int StartingConditional()
{
    object oModule = GetModule();
    return GetLocalInt(oModule, "ab_quest_started") == 1
        && GetLocalInt(oModule, "ab_has_relic") == 0;
}
