namespace UnityEditor.Rendering.HighDefinition
{
    [CustomPassDrawerAttribute(typeof(global::CopyPass))]
    public class CopyPassDrawer : CustomPassDrawer
    {
        protected override PassUIFlag commonPassUIFlags => PassUIFlag.Name;
    }
}
