void main()
{
    SetLocalInt(GetModule(), "ab_combat_won", 1);
    SendMessageToPC(GetFirstPC(), "Le gardien de la crypte est vaincu.");
}
