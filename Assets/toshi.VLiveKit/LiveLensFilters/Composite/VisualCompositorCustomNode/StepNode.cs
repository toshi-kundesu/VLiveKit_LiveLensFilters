#if VLIVEKIT_LIVELENSFILTERS_ENABLE_VISUAL_COMPOSITOR
using UnityEngine;
using Unity.VisualCompositor;

public class StepNode : CompositorNode
{
    [InputPort(name: "threshold")]
    private float threshold = 0.5f;

    public override void Render()
    {
        if (m_input == null)
        {
            if (m_output != null)
            {
                ClearRenderTexture(m_output);
            }

            return;
        }

        if (m_output == null)
        {
            m_output = new RenderTexture(m_input);
            m_output.hideFlags = HideFlags.DontSaveInEditor;
        }

        if (m_material == null)
        {
            Shader shader = Shader.Find("Custom/Step");
            m_material = new Material(shader);
            m_material.hideFlags = HideFlags.DontSaveInEditor;
        }

        m_material.SetFloat("_Threshold", threshold);
        Graphics.Blit(m_input, m_output, m_material);
    }

    private static void ClearRenderTexture(RenderTexture rt)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = previous;
    }

    [InputPort(name: "input")]
    private RenderTexture m_input;

    [OutputPort(name: "output")]
    private RenderTexture m_output;

    private Material m_material;
}
#endif
