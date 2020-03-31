using Arcen.AIW2.Core;
using Arcen.Universal;

namespace Arcen.AIW2.SK
{
    // Let the game know how to save our data.

    public class CivilianWorldExternalData : IArcenExternalDataPatternImplementation
    {
        // Make sure you use the same class name that you use for whatever data you want saved here.
        private CivilianWorld Data;

        public static int PatternIndex;

        // So this is essentially what type of thing we're going to 'attach' our class to.
        public static string RelatedParentTypeName = "World";

        public void ReceivePatternIndex(int Index)
        {
            PatternIndex = Index;
        }
        public int GetNumberOfItems()
        {
            return 1;
        }
        public bool GetShouldInitializeOn(string ParentTypeName)
        {
            // Figure out which object type has this sort of ExternalData (in this case, World)
            return ArcenStrings.Equals(ParentTypeName, RelatedParentTypeName);
        }

        public void InitializeData(object ParentObject, object[] Target)
        {
            this.Data = new CivilianWorld();
            Target[0] = this.Data;
        }
        public void SerializeExternalData(object[] Source, ArcenSerializationBuffer Buffer)
        {
            //For saving to disk, translate this object into the buffer
            CivilianWorld data = (CivilianWorld)Source[0];
            data.SerializeTo(Buffer);
        }
        public void DeserializeExternalData(object ParentObject, object[] Target, int ItemsToExpect, ArcenDeserializationBuffer Buffer)
        {
            //reverses SerializeData; gets the date out of the buffer and populates the variables
            Target[0] = new CivilianWorld(Buffer);
        }
    }

    // The following is a helper function to the above, designed to allow us to save and load data on demand.
    public static class ExtensionMethodsFor_CivilianWorld
    {
        // This loads the data assigned to whatever ParentObject you pass. So, say, you could assign the same class to different ships, and each would be able to get back the values assigned to it.
        // In our specific case here, we're going to be assigning a dictionary to every faction.
        public static CivilianWorld GetCivilianWorldExt(this World ParentObject)
        {
            return (CivilianWorld)ParentObject.ExternalData.GetCollectionByPatternIndex((int)CivilianWorldExternalData.PatternIndex).Data[0];
        }
        // This meanwhile saves the data, assigning it to whatever ParentObject you pass.
        public static void SetCivilianWorldExt(this World ParentObject, CivilianWorld data)
        {
            ParentObject.ExternalData.GetCollectionByPatternIndex((int)CivilianWorldExternalData.PatternIndex).Data[0] = data;
        }
    }

    public class CivilianFactionExternalData : IArcenExternalDataPatternImplementation
    {
        // Make sure you use the same class name that you use for whatever data you want saved here.
        private CivilianFaction Data;

        public static int PatternIndex;

        // So this is essentially what type of thing we're going to 'attach' our class to.
        public static string RelatedParentTypeName = "Faction";

        public void ReceivePatternIndex(int Index)
        {
            PatternIndex = Index;
        }
        public int GetNumberOfItems()
        {
            return 1;
        }
        public bool GetShouldInitializeOn(string ParentTypeName)
        {
            // Figure out which object type has this sort of ExternalData (in this case, Faction)
            return ArcenStrings.Equals(ParentTypeName, RelatedParentTypeName);
        }

        public void InitializeData(object ParentObject, object[] Target)
        {
            this.Data = new CivilianFaction();
            Target[0] = this.Data;
        }
        public void SerializeExternalData(object[] Source, ArcenSerializationBuffer Buffer)
        {
            //For saving to disk, translate this object into the buffer
            CivilianFaction data = (CivilianFaction)Source[0];
            data.SerializeTo(Buffer);
        }
        public void DeserializeExternalData(object ParentObject, object[] Target, int ItemsToExpect, ArcenDeserializationBuffer Buffer)
        {
            //reverses SerializeData; gets the date out of the buffer and populates the variables
            Target[0] = new CivilianFaction(Buffer);
        }
    }

    // The following is a helper function to the above, designed to allow us to save and load data on demand.
    public static class ExtensionMethodsFor_CivilianFaction
    {
        // This loads the data assigned to whatever ParentObject you pass. So, say, you could assign the same class to different ships, and each would be able to get back the values assigned to it.
        // In our specific case here, we're going to be assigning a dictionary to every faction.
        public static CivilianFaction GetCivilianFactionExt(this Faction ParentObject)
        {
            return (CivilianFaction)ParentObject.ExternalData.GetCollectionByPatternIndex((int)CivilianFactionExternalData.PatternIndex).Data[0];
        }
        // This meanwhile saves the data, assigning it to whatever ParentObject you pass.
        public static void SetCivilianFactionExt(this Faction ParentObject, CivilianFaction data)
        {
            ParentObject.ExternalData.GetCollectionByPatternIndex((int)CivilianFactionExternalData.PatternIndex).Data[0] = data;
        }
    }

    public class CivilianCargoExternalData : IArcenExternalDataPatternImplementation
    {
        // Make sure you use the same class name that you use for whatever data you want saved here.
        private CivilianCargo Data;

        public static int PatternIndex;

        // So this is essentially what type of thing we're going to 'attach' our class to.
        public static string RelatedParentTypeName = "GameEntity_Squad";

        public void ReceivePatternIndex(int Index)
        {
            PatternIndex = Index;
        }
        public int GetNumberOfItems()
        {
            return 1;
        }
        public bool GetShouldInitializeOn(string ParentTypeName)
        {
            // Figure out which object type has this sort of ExternalData (in this case, Faction)
            return ArcenStrings.Equals(ParentTypeName, RelatedParentTypeName);
        }

        public void InitializeData(object ParentObject, object[] Target)
        {
            this.Data = new CivilianCargo();
            Target[0] = this.Data;
        }
        public void SerializeExternalData(object[] Source, ArcenSerializationBuffer Buffer)
        {
            //For saving to disk, translate this object into the buffer
            CivilianCargo data = (CivilianCargo)Source[0];
            data.SerializeTo(Buffer);
        }
        public void DeserializeExternalData(object ParentObject, object[] Target, int ItemsToExpect, ArcenDeserializationBuffer Buffer)
        {
            //reverses SerializeData; gets the date out of the buffer and populates the variables
            Target[0] = new CivilianCargo(Buffer);
        }
    }

    // The following is a helper function to the above, designed to allow us to save and load data on demand.
    public static class ExtensionMethodsFor_CivilianCargo
    {
        // This loads the data assigned to whatever ParentObject you pass. So, say, you could assign the same class to different ships, and each would be able to get back the values assigned to it.
        // In our specific case here, we're going to be assigning a dictionary to every faction.
        public static CivilianCargo GetCivilianCargoExt(this GameEntity_Squad ParentObject)
        {
            return (CivilianCargo)ParentObject.ExternalData.GetCollectionByPatternIndex((int)CivilianCargoExternalData.PatternIndex).Data[0];
        }
        // This meanwhile saves the data, assigning it to whatever ParentObject you pass.
        public static void SetCivilianCargoExt(this GameEntity_Squad ParentObject, CivilianCargo data)
        {
            ParentObject.ExternalData.GetCollectionByPatternIndex((int)CivilianCargoExternalData.PatternIndex).Data[0] = data;
        }
    }

    public class CivilianStatusExternalData : IArcenExternalDataPatternImplementation
    {
        // Make sure you use the same class name that you use for whatever data you want saved here.
        private CivilianStatus Data;

        public static int PatternIndex;

        // So this is essentially what type of thing we're going to 'attach' our class to.
        public static string RelatedParentTypeName = "GameEntity_Squad";

        public void ReceivePatternIndex(int Index)
        {
            PatternIndex = Index;
        }
        public int GetNumberOfItems()
        {
            return 1;
        }
        public bool GetShouldInitializeOn(string ParentTypeName)
        {
            // Figure out which object type has this sort of ExternalData (in this case, Faction)
            return ArcenStrings.Equals(ParentTypeName, RelatedParentTypeName);
        }

        public void InitializeData(object ParentObject, object[] Target)
        {
            this.Data = new CivilianStatus();
            Target[0] = this.Data;
        }
        public void SerializeExternalData(object[] Source, ArcenSerializationBuffer Buffer)
        {
            //For saving to disk, translate this object into the buffer
            CivilianStatus data = (CivilianStatus)Source[0];
            data.SerializeTo(Buffer);
        }
        public void DeserializeExternalData(object ParentObject, object[] Target, int ItemsToExpect, ArcenDeserializationBuffer Buffer)
        {
            //reverses SerializeData; gets the date out of the buffer and populates the variables
            Target[0] = new CivilianStatus(Buffer);
        }
    }

    // The following is a helper function to the above, designed to allow us to save and load data on demand.
    public static class ExtensionMethodsFor_CivilianStatus
    {
        // This loads the data assigned to whatever ParentObject you pass. So, say, you could assign the same class to different ships, and each would be able to get back the values assigned to it.
        // In our specific case here, we're going to be assigning a dictionary to every faction.
        public static CivilianStatus GetCivilianStatusExt(this GameEntity_Squad ParentObject)
        {
            return (CivilianStatus)ParentObject.ExternalData.GetCollectionByPatternIndex((int)CivilianStatusExternalData.PatternIndex).Data[0];
        }
        // This meanwhile saves the data, assigning it to whatever ParentObject you pass.
        public static void SetCivilianStatusExt(this GameEntity_Squad ParentObject, CivilianStatus data)
        {
            ParentObject.ExternalData.GetCollectionByPatternIndex((int)CivilianStatusExternalData.PatternIndex).Data[0] = data;
        }
    }

    public class CivilianMilitiaExternalData : IArcenExternalDataPatternImplementation
    {
        // Make sure you use the same class name that you use for whatever data you want saved here.
        private CivilianMilitia Data;

        public static int PatternIndex;

        // So this is essentially what type of thing we're going to 'attach' our class to.
        public static string RelatedParentTypeName = "GameEntity_Squad";

        public void ReceivePatternIndex(int Index)
        {
            PatternIndex = Index;
        }
        public int GetNumberOfItems()
        {
            return 1;
        }
        public bool GetShouldInitializeOn(string ParentTypeName)
        {
            // Figure out which object type has this sort of ExternalData (in this case, Faction)
            return ArcenStrings.Equals(ParentTypeName, RelatedParentTypeName);
        }

        public void InitializeData(object ParentObject, object[] Target)
        {
            this.Data = new CivilianMilitia();
            Target[0] = this.Data;
        }
        public void SerializeExternalData(object[] Source, ArcenSerializationBuffer Buffer)
        {
            //For saving to disk, translate this object into the buffer
            CivilianMilitia data = (CivilianMilitia)Source[0];
            data.SerializeTo(Buffer);
        }
        public void DeserializeExternalData(object ParentObject, object[] Target, int ItemsToExpect, ArcenDeserializationBuffer Buffer)
        {
            //reverses SerializeData; gets the date out of the buffer and populates the variables
            Target[0] = new CivilianMilitia(Buffer);
        }
    }

    // The following is a helper function to the above, designed to allow us to save and load data on demand.
    public static class ExtensionMethodsFor_CivilianMilitia
    {
        // This loads the data assigned to whatever ParentObject you pass. So, say, you could assign the same class to different ships, and each would be able to get back the values assigned to it.
        // In our specific case here, we're going to be assigning a dictionary to every faction.
        public static CivilianMilitia GetCivilianMilitiaExt(this GameEntity_Squad ParentObject)
        {
            return (CivilianMilitia)ParentObject.ExternalData.GetCollectionByPatternIndex((int)CivilianMilitiaExternalData.PatternIndex).Data[0];
        }
        // This meanwhile saves the data, assigning it to whatever ParentObject you pass.
        public static void SetCivilianMilitiaExt(this GameEntity_Squad ParentObject, CivilianMilitia data)
        {
            ParentObject.ExternalData.GetCollectionByPatternIndex((int)CivilianMilitiaExternalData.PatternIndex).Data[0] = data;
        }
    }

    public class CivilianPlanetExternalData : IArcenExternalDataPatternImplementation
    {
        // Make sure you use the same class name that you use for whatever data you want saved here.
        private CivilianPlanet Data;

        public static int PatternIndex;

        // So this is essentially what type of thing we're going to 'attach' our class to.
        public static string RelatedParentTypeName = "Planet";

        public void ReceivePatternIndex( int Index )
        {
            PatternIndex = Index;
        }
        public int GetNumberOfItems()
        {
            return 1;
        }
        public bool GetShouldInitializeOn( string ParentTypeName )
        {
            // Figure out which object type has this sort of ExternalData (in this case, Faction)
            return ArcenStrings.Equals( ParentTypeName, RelatedParentTypeName );
        }

        public void InitializeData( object ParentObject, object[] Target )
        {
            this.Data = new CivilianPlanet();
            Target[0] = this.Data;
        }
        public void SerializeExternalData( object[] Source, ArcenSerializationBuffer Buffer )
        {
            //For saving to disk, translate this object into the buffer
            CivilianPlanet data = (CivilianPlanet)Source[0];
            data.SerializeTo( Buffer );
        }
        public void DeserializeExternalData( object ParentObject, object[] Target, int ItemsToExpect, ArcenDeserializationBuffer Buffer )
        {
            //reverses SerializeData; gets the date out of the buffer and populates the variables
            Target[0] = new CivilianPlanet( Buffer );
        }
    }

    // The following is a helper function to the above, designed to allow us to save and load data on demand.
    public static class ExtensionMethodsFor_CivilianPlanet
    {
        // This loads the data assigned to whatever ParentObject you pass. So, say, you could assign the same class to different ships, and each would be able to get back the values assigned to it.
        // In our specific case here, we're going to be assigning a dictionary to every faction.
        public static CivilianPlanet GetCivilianPlanetExt( this Planet ParentObject )
        {
            return (CivilianPlanet)ParentObject.ExternalData.GetCollectionByPatternIndex( (int)CivilianPlanetExternalData.PatternIndex ).Data[0];
        }
        // This meanwhile saves the data, assigning it to whatever ParentObject you pass.
        public static void SetCivilianPlanetExt( this Planet ParentObject, CivilianPlanet data )
        {
            ParentObject.ExternalData.GetCollectionByPatternIndex( (int)CivilianPlanetExternalData.PatternIndex ).Data[0] = data;
        }
    }
}
