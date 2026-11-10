void main()
{
    object oPC = GetEnteringObject();
    if (GetIsPC(oPC))
    {
        SendMessageToPC(oPC, "La crypte sent la pierre froide.");
    }
}
