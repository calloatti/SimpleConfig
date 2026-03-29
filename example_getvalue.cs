namespace MyExampleMod
{
  public class SomeFeatureManager
  {
    public void ExecuteFeature()
    {
      // 3. Read directly from the static instance using your keys
      bool isFeatureEnabled = MyModStarter.Config.GetBool("EnableFeature");
      float speedMultiplier = MyModStarter.Config.GetFloat("SpeedMultiplier");

      if (isFeatureEnabled)
      {
        // Execute your mod's logic using the speedMultiplier...
      }
    }
  }
}