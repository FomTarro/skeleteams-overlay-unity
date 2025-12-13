using Skeletom.Essentials.IO;

public class ExampleSettingsModuleA : BaseSettingsModule<ExampleSettingsManager.ExampleSettingsData>
{
    public string prop = "hello!";

    public override void FromSettingsData(ExampleSettingsManager.ExampleSettingsData data)
    {
        prop = data.propA;
    }

    public override void ToSettingsData(ExampleSettingsManager.ExampleSettingsData data)
    {
        data.propA = prop;
    }
}
