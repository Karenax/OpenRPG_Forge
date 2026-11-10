void main()
{
    object oModule = GetModule();
    if (GetLocalInt(oModule, "ab_has_relic") == 0)
    {
        SetLocalInt(oModule, "ab_has_relic", 1);
        AddJournalQuestEntry("ab_quest", 20, GetFirstPC(), TRUE);
        SendMessageToPC(GetFirstPC(), "Vous avez trouvé la relique du pont.");
    }
}
