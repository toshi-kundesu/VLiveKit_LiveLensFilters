using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using GraphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat;
using SerializableAttribute = System.SerializableAttribute;
using System.Collections.Generic;

namespace Kino.PostProcessing
{
    [Serializable, VolumeComponentMenu("Post-processing/Kino/diffusion")]
    public sealed class diffusion : CustomPostProcessVolumeComponent, IPostProcessComponent
    {
        #region Effect parameters

        // public ClampedFloatParameter threshold = new ClampedFloatParameter(1, 0, 5);
        public ClampedFloatParameter stretch = new ClampedFloatParameter(0.75f, 0, 1);
        // public ClampedFloatParameter intensity = new ClampedFloatParameter(0, 0, 1);
        public ColorParameter tint = new ColorParameter(new Color(0.55f, 0.55f, 1), false, false, true);

        /* -------- パラメータ -------- */
        public ClampedFloatParameter threshold  = new(1f, 0f, 10f);
        public ClampedFloatParameter blurRadius = new(2f, 0.1f, 10f);
        public ClampedFloatParameter intensity  = new(1f, 0f, 5f);
        public Vector4Parameter weights = new Vector4Parameter(new Vector4(0.1f, 0.2f, 0.3f, 0.4f));
        public ClampedFloatParameter exposure = new ClampedFloatParameter(1f, 0f, 10f);
        public ClampedFloatParameter contrast = new ClampedFloatParameter(1f, 0f, 10f);
        public ClampedFloatParameter saturation = new ClampedFloatParameter(1f, 0f, 10f);
        public ClampedFloatParameter bloomIntensity = new ClampedFloatParameter(1f, 0f, 10f);
        public ColorParameter bloomColor = new ColorParameter(new Color(0.55f, 0.55f, 1), false, false, true);
        // public ColorParameter        tint       = new(Color.white, false, false, true);
        #endregion

        #region Private members

        static class ShaderIDs
        {
            internal static readonly int Color = Shader.PropertyToID("_Color");
            internal static readonly int HighTexture = Shader.PropertyToID("_HighTexture");
            internal static readonly int InputTexture = Shader.PropertyToID("_InputTexture");
            // bloomTexture
            internal static readonly int BloomTextureA = Shader.PropertyToID("_BloomTextureA");
            internal static readonly int BloomTextureB = Shader.PropertyToID("_BloomTextureB");
            internal static readonly int BloomTextureC = Shader.PropertyToID("_BloomTextureC");
            internal static readonly int BloomTextureD = Shader.PropertyToID("_BloomTextureD");
            // internal static readonly int Intensity = Shader.PropertyToID("_Intensity");
            internal static readonly int SourceTexture = Shader.PropertyToID("_SourceTexture");
            // internal static readonly int Stretch = Shader.PropertyToID("_Stretch");
            
            // internal static readonly int Threshold = Shader.PropertyToID("_Threshold");
            /* -------- 内部 -------- */
        internal static readonly int _Threshold  = Shader.PropertyToID("_Threshold");
        internal static readonly int _BlurRadius = Shader.PropertyToID("_BlurRadius");
        internal static readonly int _Intensity  = Shader.PropertyToID("_Intensity");
        internal static readonly int _Tint       = Shader.PropertyToID("_Tint");
        internal static readonly int _BloomWeights = Shader.PropertyToID("_BloomWeights");
        internal static readonly int _Exposure   = Shader.PropertyToID("_Exposure");
        internal static readonly int _Contrast   = Shader.PropertyToID("_Contrast");
        internal static readonly int _Saturation = Shader.PropertyToID("_Saturation");
        internal static readonly int _BloomIntensity = Shader.PropertyToID("_BloomIntensity");
        internal static readonly int _BloomColor = Shader.PropertyToID("_BloomColor");
        internal static readonly int _BlurTexture = Shader.PropertyToID("_BlurTexture");
        // internal static readonly int _InputTex   = Shader.PropertyToID("_InputTexture");
        // internal static readonly int _SourceTex  = Shader.PropertyToID("_SourceTexture");
        }

        Material _material;
        MaterialPropertyBlock _prop;

        // Image pyramid storage
        // We have to use different pyramids for each camera, so we use a
        // dictionary and camera GUIDs as a key to store each pyramid.
        Dictionary<int, DiffusionPyramid> _pyramids;

        DiffusionPyramid GetPyramid(HDCamera camera)
        {
            DiffusionPyramid candid;
            var cameraID = camera.camera.GetInstanceID();

            if (_pyramids.TryGetValue(cameraID, out candid))
            {
                // Reallocate the RTs when the screen size was changed.
                if (!candid.CheckSize(camera)) candid.Reallocate(camera);
            }
            else
            {
                // No one found: Allocate a new pyramid.
                _pyramids[cameraID] = candid = new DiffusionPyramid(camera);
            }

            return candid;
        }

        #endregion

        #region IPostProcessComponent implementation

        public bool IsActive() => _material != null && intensity.value > 0;

        #endregion

        #region CustomPostProcessVolumeComponent implementation

        public override CustomPostProcessInjectionPoint injectionPoint =>
            CustomPostProcessInjectionPoint.BeforePostProcess;

        public override void Setup()
        {
            _material = CoreUtils.CreateEngineMaterial("Hidden/Kino/PostProcess/diffusion");
            _prop = new MaterialPropertyBlock();
            _pyramids = new Dictionary<int, DiffusionPyramid>();
        }

        public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle srcRT, RTHandle destRT)
        {
            var pyramid = GetPyramid(camera);

            // Common parameters
            _material.SetFloat("_Threshold", threshold.value);
            _material.SetFloat("_Stretch", stretch.value);
            _material.SetFloat("_Intensity", intensity.value);
            _material.SetColor("_Color", tint.value);
            _material.SetTexture("_SourceTexture", srcRT);
            _material.SetFloat("_BlurRadius", blurRadius.value);
            // _BloomWeights
            _material.SetVector("_BloomWeights", weights.value);
            _material.SetFloat("_Exposure", exposure.value);
            _material.SetFloat("_Contrast", contrast.value);
            _material.SetFloat("_Saturation", saturation.value);
            _material.SetFloat("_BloomIntensity", bloomIntensity.value);
            _material.SetColor("_BloomColor", bloomColor.value);
            // _material.SetTexture("_BlurTex", pyramid[0].A);
            // Source -> Prefilter -> MIP 0
            // まず、コントラストのパスに通す
            HDUtils.DrawFullScreen(cmd, _material, pyramid[0].TempBlurBuffer1, _prop, 14);
            _prop.SetTexture(ShaderIDs.InputTexture, pyramid[0].TempBlurBuffer1);
            HDUtils.DrawFullScreen(cmd, _material, pyramid[0].TempBlurBuffer2, _prop, 15);
            _prop.SetTexture(ShaderIDs.InputTexture, pyramid[0].TempBlurBuffer2);
            HDUtils.DrawFullScreen(cmd, _material, pyramid[0].TempBlurBuffer1, _prop, 16);
            _prop.SetTexture(ShaderIDs._BlurTexture, pyramid[0].TempBlurBuffer1);
            HDUtils.DrawFullScreen(cmd, _material, destRT, _prop, 17);


            // // upにも書き込む
            // _prop.SetTexture(ShaderIDs.InputTexture, pyramid[0].A);
            // // horizontal blur1x
            // HDUtils.DrawFullScreen(cmd, _material, pyramid[0].B, _prop, 7);
            // _prop.SetTexture(ShaderIDs.InputTexture, pyramid[0].B);
            // // vertical blur1x
            // HDUtils.DrawFullScreen(cmd, _material, pyramid[0].A, _prop, 9);
            // // blur2x
           
            // // _prop.SetTexture(ShaderIDs.InputTexture, pyramid[0].A);
            // // HDUtils.DrawFullScreen(cmd, _material, pyramid[0].B, _prop, 7);
            // // HDUtils.DrawFullScreen(cmd, _material, pyramid[0].up, _prop, 7);
            // // output input texture

            

            // var level = 1;
            // for (; level < DiffusionPyramid.MaxMipLevel && pyramid[level].TempBlurBuffer1 != null; level++)
            // {
            //     // horizontal blur2x
            //     _prop.SetTexture(ShaderIDs.InputTexture, pyramid[level - 1].TempBlurBuffer1);
            //     HDUtils.DrawFullScreen(cmd, _material, pyramid[level].TempBlurBuffer2, _prop, 15);
            //     _prop.SetTexture(ShaderIDs.InputTexture, pyramid[level].TempBlurBuffer2);
            //     HDUtils.DrawFullScreen(cmd, _material, pyramid[level].TempBlurBuffer1, _prop, 16);
                
            //     // _prop.SetTexture(ShaderIDs.InputTexture, pyramid[i - 1].A);
            //     // HDUtils.DrawFullScreen(cmd, _material, pyramid[i].A, _prop, 7);
            //     // _prop.SetTexture(ShaderIDs.InputTexture, pyramid[i].A);
            //     // HDUtils.DrawFullScreen(cmd, _material, pyramid[i].B, _prop, 9);
            // }
            // _prop.SetTexture(ShaderIDs.BloomTextureA, pyramid[0].TempBlurBuffer1);
            // _prop.SetTexture(ShaderIDs.BloomTextureB, pyramid[1].TempBlurBuffer1);
            // _prop.SetTexture(ShaderIDs.BloomTextureC, pyramid[2].TempBlurBuffer1);
            // _prop.SetTexture(ShaderIDs.BloomTextureD, pyramid[3].TempBlurBuffer1);

            // // _prop.SetTexture(ShaderIDs.InputTexture, pyramid[3].A);
            // HDUtils.DrawFullScreen(cmd, _material, pyramid[0].TempBlurBuffer2, _prop, 17);
            // _prop.SetTexture(ShaderIDs.BloomTextureA, pyramid[0].TempBlurBuffer2);

            // HDUtils.DrawFullScreen(cmd, _material, destRT, _prop, 13);

            // Downsample
            // var level = 1;
            // for (; level < GenshinBloomPyramid.MaxMipLevel && pyramid[level].A != null; level++)
            // {
            //     // mip.down = bufferA
            //     // mip.up = bufferB
            //     // BloomHorizontalBlur1x
            //     _prop.SetTexture(ShaderIDs.InputTexture, pyramid[level - 1].A);
            //     HDUtils.DrawFullScreen(cmd, _material, pyramid[level].A, _prop, 7);
            // }

            // // Upsample & combine
            // var lastRT = pyramid[--level].A;
            // for (level--; level >= 1; level--)
            // {
            //     var mip = pyramid[level];
            //     _prop.SetTexture(ShaderIDs.InputTexture, lastRT);
            //     _prop.SetTexture(ShaderIDs.HighTexture, mip.A);
            //     HDUtils.DrawFullScreen(cmd, _material, mip.B, _prop, 2);
            //     lastRT = mip.B;
            // }

            // // Final composition
            // _prop.SetTexture(ShaderIDs.InputTexture, lastRT);
            // HDUtils.DrawFullScreen(cmd, _material, destRT, _prop, 3);
        }

        public override void Cleanup()
        {
            CoreUtils.Destroy(_material);
            foreach (var pyramid in _pyramids.Values) pyramid.Release();
        }

        #endregion
    }

    #region Image pyramid class used in GenshinBloom effect

    sealed class DiffusionPyramid
    {
        public const int MaxMipLevel = 16;

        int _baseWidth, _baseHeight;
        readonly (RTHandle TempBlurBuffer1, RTHandle TempBlurBuffer2) [] _mips = new (RTHandle, RTHandle) [MaxMipLevel];

        public (RTHandle TempBlurBuffer1, RTHandle TempBlurBuffer2) this [int index]
        {
            get { return _mips[index]; }
        }

        public DiffusionPyramid(HDCamera camera)
        {
            Allocate(camera);
        }

        public bool CheckSize(HDCamera camera)
        {
            return _baseWidth == camera.actualWidth && _baseHeight == camera.actualHeight;
        }

        public void Reallocate(HDCamera camera)
        {
            Release();
            Allocate(camera);
        }

        public void Release()
        {
            foreach (var mip in _mips)
            {
                if (mip.TempBlurBuffer1 != null) RTHandles.Release(mip.TempBlurBuffer1);
                if (mip.TempBlurBuffer2 != null) RTHandles.Release(mip.TempBlurBuffer2);
            }
        }

        void Allocate(HDCamera camera)
        {
            _baseWidth = camera.actualWidth;
            _baseHeight = camera.actualHeight;

            var width = _baseWidth / 2;
            var height = _baseHeight / 2;

            const GraphicsFormat RTFormat = GraphicsFormat.R16G16B16A16_SFloat;

            // ★ down と up の両方を確保
            _mips[0] = (RTHandles.Alloc(width, height, colorFormat: RTFormat),
                RTHandles.Alloc(width, height, colorFormat: RTFormat));

            for (var i = 1; i < MaxMipLevel; i++)
            {
                width /= 2;
                height /= 2;
                _mips[i] = width < 4 ?  (null, null) :
                    (RTHandles.Alloc(width, height, colorFormat: RTFormat),
                     RTHandles.Alloc(width, height, colorFormat: RTFormat));
            }
        }
    }

    #endregion
}
