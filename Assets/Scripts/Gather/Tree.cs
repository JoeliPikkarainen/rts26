public class Tree : GenericNode
{
    void Awake()
    {
        ApplyIdentityDefaults("Tree", GatherResourcePreference.Tree);
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        ApplyIdentityDefaults("Tree", GatherResourcePreference.Tree);
    }
}