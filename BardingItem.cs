namespace BuddyDresser;

public record BardingItem(
    uint RowId,
    string Name,
    int ModelHead,
    int ModelBody,
    int ModelLegs,
    ushort IconHead,
    ushort IconBody,
    ushort IconLegs
)
{
    public bool HasHeadPiece => ModelHead > 0 || RowId == 0;
    public bool HasBodyPiece => ModelBody > 0 || RowId == 0;
    public bool HasLegsPiece => ModelLegs > 0 || RowId == 0;
}
