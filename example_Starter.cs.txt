using Timberborn.ModManagerScene;
using Calloatti.Config;

namespace MyExampleMod
{
  public class MyModStarter : IModStarter
  {
    // 1. Declare the globally accessible static instance
    public static SimpleConfig Config { get; private set; }

    public void StartMod(IModEnvironment modEnvironment)
    {
      // 2. Instantiate the config. This instantly runs the TXT synchronization.
      Config = new SimpleConfig(modEnvironment.ModPath);
      
      // The rest of your mod's initialization goes here...
    }
  }
}