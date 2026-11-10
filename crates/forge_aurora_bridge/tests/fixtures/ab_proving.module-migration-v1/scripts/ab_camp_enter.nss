void main()
{
    object oPC = GetEnteringObject();
    if (GetIsPC(oPC))
    {
        SendMessageToPC(oPC, "Le vent souffle sur le pont rompu.");
        if (GetLocalInt(GetModule(), "ab_journal_available") == 0)
        {
            AddJournalQuestEntry("ab_quest", 10, oPC, TRUE);
            SetLocalInt(GetModule(), "ab_journal_available", 1);
        }
    }
}
