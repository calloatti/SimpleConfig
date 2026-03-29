using Calloatti.Config;

namespace MyMod
{
    public class LogicManager
    {
        public void ApplyNewSettings()
        {
            // Set as many values as you want. 
            // This only updates the dictionary in memory, so it's instantly fast.
            ModMain.Config.Set("FeatureEnabled", true);
            ModMain.Config.Set("SpeedMultiplier", 2.5f);
            ModMain.Config.Set("PlayerName", "BeaverBob");

            // Write all the new values to the .txt file in one go.
            ModMain.Config.Save();
        }
    }
}