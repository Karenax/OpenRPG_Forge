void main()
{
    object oModule = GetModule();
    object oPC = GetFirstPC();
    if (GetLocalInt(oModule, "ab_has_relic") == 1
        && GetLocalInt(oModule, "ab_quest_complete") == 0)
    {
        SetLocalInt(oModule, "ab_quest_complete", 1);
        if (GetLocalInt(oModule, "ab_reward_given") == 0)
        {
            SetLocalInt(oModule, "ab_reward_given", 1);
            GiveGoldToCreature(oPC, 50);
            AddJournalQuestEntry("ab_quest", 30, oPC, TRUE);
        }
    }
}
