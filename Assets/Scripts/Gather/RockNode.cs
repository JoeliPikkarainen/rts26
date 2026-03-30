public class RockNode : GenericNode
{
	void Awake()
	{
		ApplyIdentityDefaults("Rock", GatherResourcePreference.Rock);
	}

	protected override void OnValidate()
	{
		base.OnValidate();
		ApplyIdentityDefaults("Rock", GatherResourcePreference.Rock);
	}
}
