using Skeletom.Essentials.IO;

public class ExampleSettingsModuleB : BaseSettingsModule<ExampleSettingsManager.ExampleSettingsData>
{
    public string prop = "goodbye!";
    public override void FromSettingsData(ExampleSettingsManager.ExampleSettingsData data)
    {
        prop = data.propB;
    }

    public override void ToSettingsData(ExampleSettingsManager.ExampleSettingsData data)
    {
        data.propB = prop;
    }
}
