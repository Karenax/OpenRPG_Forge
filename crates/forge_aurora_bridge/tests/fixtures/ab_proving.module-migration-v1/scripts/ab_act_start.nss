void main()
{
    object oModule = GetModule();
    if (GetLocalInt(oModule, "ab_quest_started") == 0)
    {
        SetLocalInt(oModule, "ab_quest_started", 1);
        AddJournalQuestEntry("ab_quest", 20, GetFirstPC(), TRUE);
    }
}
