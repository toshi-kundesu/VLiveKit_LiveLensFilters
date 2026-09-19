namespace UnityEditor.Rendering.HighDefinition
{
    [CustomPassDrawerAttribute(typeof(global::LayerLightWrap))]
    public sealed class LayerLightWrapDrawer : CustomPassDrawer
    {
        protected override PassUIFlag commonPassUIFlags => PassUIFlag.Name;
    }
}
