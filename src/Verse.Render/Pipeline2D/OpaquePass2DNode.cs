using Verse.ECS;
using Verse.MoonWorks.Graphics;
using Verse.MoonWorks.Graphics.Resources;
using Verse.Render.Graph;
using Verse.Render.Graph.RenderPhase;
using Verse.Render.View;

namespace Verse.Render.Pipeline2D;

public class OpaquePass2DNode : IViewRenderNode<Empty>
{
	public OpaquePass2DNode(GraphicsDevice device)
	{
		/**
		var info = new GraphicsPipelineCreateInfo
		{
			TargetInfo = new GraphicsPipelineTargetInfo
			{
				ColorTargetDescriptions =
				[
					new ColorTargetDescription
					{
						Format = swapchainFormat,
						BlendState = ColorTargetBlendState.Opaque
					}
				]
			},
			DepthStencilState = DepthStencilState.Disable,
			MultisampleState = MultisampleState.None,
			PrimitiveType = PrimitiveType.TriangleList,
			RasterizerState = RasterizerState.CCW_CullNone,
			VertexInputState = VertexInputState.Empty,
			VertexShader = vertShader,
			FragmentShader = fragShader
		};
		pipelineCreateInfo.VertexInputState = VertexInputState.CreateSingleBinding<PositionTextureVertex>();
		_pipeline = GraphicsPipeline.Create(GraphicsDevice, pipelineCreateInfo);	
		
		**/
	}

	public void Run(RenderGraphContext context, RenderContext renderContext, World world, EntityView viewEntity)
	{
		var camera = viewEntity.Get<ExtractedCamera>();
		var view = viewEntity.Get<ExtractedView>();
		var target = viewEntity.Get<ViewTargetTexture>();
		var depth = viewEntity.Get<ViewDepthTexture>();
		
		var opaquePhases = world.GetRes<ViewBinnedRenderPhases<Opaque2D>>()
		
		renderContext.AddCommandBufferFunc(device => {
			var buffer = device.AcquireCommandBuffer();
			Texture? targetTexture;
			if (target.Window != null) {
				targetTexture = buffer.WaitAndAcquireSwapchainTexture(target.Window);
			} else {
				targetTexture = target.Texture;
			}
			if (targetTexture == null)
			{
				return buffer;
			}
			
			var renderPass = buffer.BeginRenderPass(
				new ColorTargetInfo(targetTexture, Color.CornflowerBlue)
			);
			/**
			// iterate over each bind group
			renderPass.BindGraphicsPipeline(_pipeline);
			renderPass.BindVertexBuffers(vertexBuffer);
			renderPass.BindIndexBuffer(indexBuffer, IndexElementSize.Sixteen);
			renderPass.BindFragmentSamplers(new TextureSamplerBinding(testTex, samplers[samplerIndex]));
			// submit all batches in that bind group
			renderPass.DrawIndexedPrimitives(6, 1, 0, 0, 0);
			**/
			buffer.EndRenderPass(renderPass);
			
			return buffer;
		});
	}
}