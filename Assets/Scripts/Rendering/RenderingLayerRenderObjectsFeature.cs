using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class RenderingLayerRenderObjectsFeature : ScriptableRendererFeature
{
    [SerializeField] private string passTag = "RenderingLayerRenderObjects";
    [SerializeField] private RenderPassEvent outlineEvent = RenderPassEvent.AfterRenderingOpaques;
    [SerializeField] private RenderPassEvent depthEvent = RenderPassEvent.BeforeRenderingTransparents;
    [SerializeField] private RenderQueueType renderQueueType = RenderQueueType.Opaque;
    [SerializeField] private LayerMask gameObjectLayerMask = ~0;
    [SerializeField] private uint renderingLayerMask = 1u << 1;
    [SerializeField] private Material overrideMaterial;
    [SerializeField] private int overrideMaterialPassIndex;
    [SerializeField] private CompareFunction outlineDepthCompareFunction = CompareFunction.LessEqual;

    private RenderingLayerRenderPass _outlinePass;
    private RenderingLayerRenderPass _depthPass;

    public override void Create()
    {
        _outlinePass = new RenderingLayerRenderPass(passTag, outlineEvent, renderQueueType, gameObjectLayerMask, renderingLayerMask, overrideMaterial, overrideMaterialPassIndex, true, false, outlineDepthCompareFunction, RenderingLayerRenderPass.DefaultShaderTagIds);
        _depthPass = new RenderingLayerRenderPass($"{passTag} Depth", depthEvent, renderQueueType, gameObjectLayerMask, renderingLayerMask, null, 0, false, true, CompareFunction.LessEqual, RenderingLayerRenderPass.ForwardShaderTagIds);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(_outlinePass);
        renderer.EnqueuePass(_depthPass);
    }

    private sealed class RenderingLayerRenderPass : ScriptableRenderPass
    {
        public static readonly List<ShaderTagId> DefaultShaderTagIds = new()
        {
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly")
        };

        public static readonly List<ShaderTagId> ForwardShaderTagIds = new()
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly")
        };

        private readonly string _passTag;
        private readonly RenderQueueType _renderQueueType;
        private readonly LayerMask _gameObjectLayerMask;
        private readonly uint _renderingLayerMask;
        private readonly Material _overrideMaterial;
        private readonly int _overrideMaterialPassIndex;
        private readonly RenderStateBlock _renderStateBlock;
        private readonly List<ShaderTagId> _shaderTagIds;

        public RenderingLayerRenderPass(string passTag, RenderPassEvent renderPassEvent, RenderQueueType renderQueueType, LayerMask gameObjectLayerMask, uint renderingLayerMask, Material overrideMaterial, int overrideMaterialPassIndex, bool overrideDepthState, bool depthWriteEnabled, CompareFunction depthCompareFunction, List<ShaderTagId> shaderTagIds)
        {
            _passTag = passTag;
            _renderQueueType = renderQueueType;
            _gameObjectLayerMask = gameObjectLayerMask;
            _renderingLayerMask = renderingLayerMask;
            _overrideMaterial = overrideMaterial;
            _overrideMaterialPassIndex = overrideMaterialPassIndex;
            _shaderTagIds = shaderTagIds;
            this.renderPassEvent = renderPassEvent;
            profilingSampler = new ProfilingSampler(passTag);

            _renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
            if (overrideDepthState)
            {
                _renderStateBlock.mask |= RenderStateMask.Depth;
                _renderStateBlock.depthState = new DepthState(depthWriteEnabled, depthCompareFunction);
            }
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            using var builder = renderGraph.AddRasterRenderPass<PassData>(_passTag, out PassData passData, profilingSampler);

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();

            SortingCriteria sortingCriteria = GetSortingCriteria(cameraData.defaultOpaqueSortFlags);
            DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(_shaderTagIds, renderingData, cameraData, lightData, sortingCriteria);
            ApplyOverrideMaterial(ref drawingSettings);

            FilteringSettings filteringSettings = CreateFilteringSettings();
            RendererListParams rendererListParams = new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings);
            if (_renderStateBlock.mask != RenderStateMask.Nothing)
            {
                rendererListParams.tagValues = new NativeArray<ShaderTagId>(new[] { ShaderTagId.none }, Allocator.Temp);
                rendererListParams.stateBlocks = new NativeArray<RenderStateBlock>(new[] { _renderStateBlock }, Allocator.Temp);
                rendererListParams.isPassTagName = false;
            }

            passData.RendererList = renderGraph.CreateRendererList(rendererListParams);
            if (!passData.RendererList.IsValid())
                return;

            builder.UseRendererList(passData.RendererList);
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);
            UseLightingTextures(builder, resourceData);
            builder.AllowGlobalStateModification(true);
            builder.SetRenderFunc(static (PassData data, RasterGraphContext context) => context.cmd.DrawRendererList(data.RendererList));
        }

        private static void UseLightingTextures(IBaseRenderGraphBuilder builder, UniversalResourceData resourceData)
        {
            TextureHandle mainShadowsTexture = resourceData.mainShadowsTexture;
            if (mainShadowsTexture.IsValid())
                builder.UseTexture(mainShadowsTexture, AccessFlags.Read);

            TextureHandle additionalShadowsTexture = resourceData.additionalShadowsTexture;
            if (additionalShadowsTexture.IsValid())
                builder.UseTexture(additionalShadowsTexture, AccessFlags.Read);

            TextureHandle[] dBufferHandles = resourceData.dBuffer;
            for (int i = 0; i < dBufferHandles.Length; i++)
            {
                TextureHandle dBuffer = dBufferHandles[i];
                if (dBuffer.IsValid())
                    builder.UseTexture(dBuffer, AccessFlags.Read);
            }

            TextureHandle ssaoTexture = resourceData.ssaoTexture;
            if (ssaoTexture.IsValid())
                builder.UseTexture(ssaoTexture, AccessFlags.Read);
        }

        private FilteringSettings CreateFilteringSettings()
        {
            RenderQueueRange renderQueueRange = _renderQueueType == RenderQueueType.Transparent ? RenderQueueRange.transparent : RenderQueueRange.opaque;
            FilteringSettings filteringSettings = new FilteringSettings(renderQueueRange, _gameObjectLayerMask);
            filteringSettings.renderingLayerMask = _renderingLayerMask;
            return filteringSettings;
        }

        private SortingCriteria GetSortingCriteria(SortingCriteria defaultOpaqueSortFlags)
        {
            return _renderQueueType == RenderQueueType.Transparent ? SortingCriteria.CommonTransparent : defaultOpaqueSortFlags;
        }

        private void ApplyOverrideMaterial(ref DrawingSettings drawingSettings)
        {
            drawingSettings.overrideMaterial = _overrideMaterial;
            drawingSettings.overrideMaterialPassIndex = _overrideMaterialPassIndex;
        }

        private sealed class PassData
        {
            public RendererListHandle RendererList;
        }
    }
}
