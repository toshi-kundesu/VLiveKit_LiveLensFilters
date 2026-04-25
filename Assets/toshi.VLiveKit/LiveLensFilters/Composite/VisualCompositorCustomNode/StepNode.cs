using UnityEngine;
using Unity.VisualCompositor;

public class StepNode : CompositorNode {
    [InputPort(name: "threshold")] private float threshold = 0.5f; // 閾値の入力ポートを追加

    public override void Render() {
        if (null == m_input) {
            if (null != m_output) {
                ClearRenderTexture(m_output);                
            }
            return;            
        }

        if (null == m_output) {
            m_output           = new RenderTexture(m_input);
            m_output.hideFlags = HideFlags.DontSaveInEditor;
        }

        if(m_material == null) {
            Shader shader = Shader.Find("Custom/Step");
            m_material           = new Material(shader);
            m_material.hideFlags = HideFlags.DontSaveInEditor;
        } 

        m_material.SetFloat("_Threshold", threshold); // 閾値をシェーダーに渡す
        Graphics.Blit(m_input, m_output, m_material);
    }

    private static void ClearRenderTexture(RenderTexture rt) {
        RenderTexture prevRT = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = prevRT;        
    }    
//--------------------------------------------------------------------------------------------------------------------------------------------------------------    
    
    [InputPort(name: "input")] private RenderTexture m_input;
    [OutputPort(name: "output")] private RenderTexture m_output;

    private Material m_material;
}