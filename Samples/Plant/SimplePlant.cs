using System.Drawing;
using System.IO;
using OrganismBase;

// Sample Plant
// Strategy: plants don't act.  The game grows them, and they reproduce on
// their own whenever they can, so all a plant decides is what it's made of.
namespace Plant
{
    // Larger plants hold more food but take longer to grow and reproduce
    [MatureSize(36)]

    // Plants get 100 points too; energy is all a plant needs
    [MaximumEnergyPoints(100)]

    // Required, though the current Terrarium places seedlings itself
    [SeedSpreadDistance(100)]

    [PlantSkin(PlantSkinFamily.PlantOne)]
    [MarkingColor(KnownColor.Green)]
    public class SimplePlant : OrganismBase.Plant
    {
        // A plant has nothing to remember when it is saved
        public override void SerializePlant(MemoryStream m)
        {
        }

        public override void DeserializePlant(MemoryStream m)
        {
        }
    }
}
