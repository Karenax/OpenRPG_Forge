void main()
{
    object oModule = GetModule();
    if (GetLocalInt(oModule, "ab_initialized") == 0)
    {
        SetLocalInt(oModule, "ab_quest_started", 0);
        SetLocalInt(oModule, "ab_has_relic", 0);
        SetLocalInt(oModule, "ab_combat_won", 0);
        SetLocalInt(oModule, "ab_quest_complete", 0);
        SetLocalInt(oModule, "ab_reward_given", 0);
        SetLocalInt(oModule, "ab_initialized", 1);
    }
}
