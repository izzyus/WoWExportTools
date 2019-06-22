﻿using System;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using WoWExportTools.Loaders;
using System.Drawing;
using OpenTK.Input;
using System.Collections.Generic;
using static WoWExportTools.Structs;
using CASCLib;

namespace WoWExportTools
{
    public class PreviewControl
    {
        public GLControl renderCanvas;

        private bool ready = false;
        private string modelType;

        private CacheStorage cache = new CacheStorage();

        private NewCamera ActiveCamera;

        private string filename;

        private int adtShaderProgram;
        private int wmoShaderProgram;
        private int m2ShaderProgram;
        private int bakeShaderProgram;
        private int bakeFullMinimapShaderProgram;

        private List<string> adtList;

        public PreviewControl(GLControl renderCanvas)
        {
            this.renderCanvas = renderCanvas;
            this.renderCanvas.Paint += RenderCanvas_Paint;
            this.renderCanvas.Load += RenderCanvas_Load;
            this.renderCanvas.Resize += RenderCanvas_Resize;

            ActiveCamera = new NewCamera(renderCanvas.Width, renderCanvas.Height, new Vector3(0, 0, -1), new Vector3(-11, 0, 0));
        }

        public void SetCamera(float x, float y, float z, float rot)
        {
            ActiveCamera.Pos = new Vector3(x, y, z);
            ActiveCamera.rotationAngle = rot;
        }

        private void RenderCanvas_Resize(object sender, EventArgs e)
        {
            GL.Viewport(0, 0, renderCanvas.Width, renderCanvas.Height);
            if(renderCanvas.Width > 0 && renderCanvas.Height > 0)
            {
                ActiveCamera.viewportSize(renderCanvas.Width, renderCanvas.Height);
            }
        }

        public void BakeTexture(MapTile mapTile, string outname, bool minimap = false)
        {
            var minimapRenderer = new Renderer.RenderMinimap();
            if (minimap)
            {
                minimapRenderer.Generate(mapTile, outname, cache, bakeFullMinimapShaderProgram);
            }
            else
            {
                minimapRenderer.Generate(mapTile, outname, cache, bakeShaderProgram);
            }
        }

        public void LoadModel(string filename)
        {
            ready = false;
            GL.ActiveTexture(TextureUnit.Texture0);

            this.filename = filename;
            try
            {
                if (filename.EndsWith(".m2"))
                {
                    if (!cache.doodadBatches.ContainsKey(filename))
                    {
                        M2Loader.LoadM2(filename, cache, m2ShaderProgram);
                    }

                    if (!cache.doodadBatches.ContainsKey(filename))
                    {
                        return;
                    }

                    ActiveCamera.Pos = new Vector3((cache.doodadBatches[filename].boundingBox.max.Z) + 11.0f, 0.0f, 4.0f);
                    modelType = "m2";

                    ready = true;
                }
                else if (filename.EndsWith(".wmo"))
                {
                    if (!cache.worldModels.ContainsKey(filename))
                    {
                        WMOLoader.LoadWMO(filename, cache, wmoShaderProgram);
                    }
                    modelType = "wmo";

                    ready = true;
                }
            }catch(Exception e)
            {
                Logger.WriteLine("Error occured when loading model " + filename + ": " + e.StackTrace);
            }
           
            ActiveCamera.ResetCamera();
        }

        public void WindowsFormsHost_Initialized(object sender, EventArgs e)
        {
            renderCanvas.MakeCurrent();
        }

        private void Update()
        {
            if (!renderCanvas.Focused) return;

            var mouseState = Mouse.GetState();
            var keyboardState = Keyboard.GetState();

            ActiveCamera.processKeyboardInput(keyboardState);

            return;
        }

        private void RenderCanvas_Load(object sender, EventArgs e)
        {
            GL.Enable(EnableCap.DepthTest);

            adtShaderProgram = Shader.CompileShader("adt");
            wmoShaderProgram = Shader.CompileShader("wmo");
            m2ShaderProgram = Shader.CompileShader("m2");
            bakeShaderProgram = Shader.CompileShader("baketexture");
            bakeFullMinimapShaderProgram = Shader.CompileShader("bakeFullMinimap");

            GL.ClearColor(Color.Black);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        }

        private void RenderCanvas_Paint(object sender, System.Windows.Forms.PaintEventArgs e)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            if (!ready) return;

            GL.Viewport(0, 0, renderCanvas.Width, renderCanvas.Height);
            GL.Enable(EnableCap.Texture2D);

            if (modelType == "m2")
            {
                GL.UseProgram(m2ShaderProgram);

                ActiveCamera.setupGLRenderMatrix(m2ShaderProgram);
                ActiveCamera.flyMode = false;

                var alphaRefLoc = GL.GetUniformLocation(m2ShaderProgram, "alphaRef");

                GL.BindVertexArray(cache.doodadBatches[filename].vao);

                for (var i = 0; i < cache.doodadBatches[filename].submeshes.Length; i++)
                {
                    switch (cache.doodadBatches[filename].submeshes[i].blendType)
                    {
                        case 0:
                            GL.Disable(EnableCap.Blend);
                            GL.Uniform1(alphaRefLoc, -1.0f);
                            break;
                        case 1:
                            GL.Disable(EnableCap.Blend);
                            GL.Uniform1(alphaRefLoc, 0.90393700787f);
                            break;
                        case 2:
                            GL.Enable(EnableCap.Blend);
                            GL.Uniform1(alphaRefLoc, -1.0f);
                            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                            break;
                        default:
                            GL.Disable(EnableCap.Blend);
                            GL.Uniform1(alphaRefLoc, -1.0f);
                            break;
                    }

                    GL.BindTexture(TextureTarget.Texture2D, cache.doodadBatches[filename].submeshes[i].material);
                    GL.DrawElements(PrimitiveType.Triangles, (int)cache.doodadBatches[filename].submeshes[i].numFaces, DrawElementsType.UnsignedInt, (int)cache.doodadBatches[filename].submeshes[i].firstFace * 4);
                }
            }
            else if (modelType == "wmo")
            {
                GL.UseProgram(wmoShaderProgram);

                ActiveCamera.setupGLRenderMatrix(wmoShaderProgram);
                ActiveCamera.flyMode = false;

                var alphaRefLoc = GL.GetUniformLocation(wmoShaderProgram, "alphaRef");

                for (var j = 0; j < cache.worldModelBatches[filename].wmoRenderBatch.Length; j++)
                {
                    GL.BindVertexArray(cache.worldModelBatches[filename].groupBatches[cache.worldModelBatches[filename].wmoRenderBatch[j].groupID].vao);

                    switch(cache.worldModelBatches[filename].wmoRenderBatch[j].blendType)
                    {
                        case 0:
                            GL.Disable(EnableCap.Blend);
                            GL.Uniform1(alphaRefLoc, -1.0f);
                            break;
                        case 1:
                            GL.Disable(EnableCap.Blend);
                            GL.Uniform1(alphaRefLoc, 0.90393700787f);
                            break;
                        case 2:
                            GL.Enable(EnableCap.Blend);
                            GL.Uniform1(alphaRefLoc, -1.0f);
                            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                            break;
                        default:
                            GL.Disable(EnableCap.Blend);
                            GL.Uniform1(alphaRefLoc, -1.0f);
                            break;
                    }

                    GL.BindTexture(TextureTarget.Texture2D, cache.worldModelBatches[filename].wmoRenderBatch[j].materialID[0]);
                    GL.DrawElements(PrimitiveType.Triangles, (int)cache.worldModelBatches[filename].wmoRenderBatch[j].numFaces, DrawElementsType.UnsignedInt, (int)cache.worldModelBatches[filename].wmoRenderBatch[j].firstFace * 4);
                }
            }
            else if(modelType == "adt")
            {
                GL.UseProgram(adtShaderProgram);

                ActiveCamera.setupGLRenderMatrix(adtShaderProgram);
                ActiveCamera.flyMode = true;

                var heightScaleLoc = GL.GetUniformLocation(adtShaderProgram, "pc_heightScale");
                var heightOffsetLoc = GL.GetUniformLocation(adtShaderProgram, "pc_heightOffset");

                foreach(var filename in adtList)
                {
                    GL.BindVertexArray(cache.terrain[filename].vao);

                    for (var i = 0; i < cache.terrain[filename].renderBatches.Length; i++)
                    {
                        GL.Uniform4(heightScaleLoc, cache.terrain[filename].renderBatches[i].heightScales);
                        GL.Uniform4(heightOffsetLoc, cache.terrain[filename].renderBatches[i].heightOffsets);

                        for (var j = 0; j < cache.terrain[filename].renderBatches[i].materialID.Length; j++)
                        {
                            var textureLoc = GL.GetUniformLocation(adtShaderProgram, "pt_layer" + j);
                            GL.Uniform1(textureLoc, j);

                            var scaleLoc = GL.GetUniformLocation(adtShaderProgram, "layer" + j + "scale");
                            GL.Uniform1(scaleLoc, cache.terrain[filename].renderBatches[i].scales[j]);

                            GL.ActiveTexture(TextureUnit.Texture0 + j);
                            GL.BindTexture(TextureTarget.Texture2D, (int)cache.terrain[filename].renderBatches[i].materialID[j]);
                        }

                        for (var j = 1; j < cache.terrain[filename].renderBatches[i].alphaMaterialID.Length; j++)
                        {
                            var textureLoc = GL.GetUniformLocation(adtShaderProgram, "pt_blend" + j);
                            GL.Uniform1(textureLoc, 3 + j);

                            GL.ActiveTexture(TextureUnit.Texture3 + j);
                            GL.BindTexture(TextureTarget.Texture2D, cache.terrain[filename].renderBatches[i].alphaMaterialID[j]);
                        }

                        for (var j = 0; j < cache.terrain[filename].renderBatches[i].heightMaterialIDs.Length; j++)
                        {
                            var textureLoc = GL.GetUniformLocation(adtShaderProgram, "pt_height" + j);
                            GL.Uniform1(textureLoc, 7 + j);

                            GL.ActiveTexture(TextureUnit.Texture7 + j);
                            GL.BindTexture(TextureTarget.Texture2D, cache.terrain[filename].renderBatches[i].heightMaterialIDs[j]);
                        }

                        GL.DrawElements(PrimitiveType.Triangles, (int)cache.terrain[filename].renderBatches[i].numFaces, DrawElementsType.UnsignedInt, (int)cache.terrain[filename].renderBatches[i].firstFace * 4);

                        for (var j = 0; j < 11; j++)
                        {
                            GL.ActiveTexture(TextureUnit.Texture0 + j);
                            GL.BindTexture(TextureTarget.Texture2D, 0);
                        }

                        GL.DrawRangeElements(PrimitiveType.Triangles, (int)cache.terrain[filename].renderBatches[i].firstFace, (int)cache.terrain[filename].renderBatches[i].firstFace + (int)cache.terrain[filename].renderBatches[i].numFaces, (int)cache.terrain[filename].renderBatches[i].numFaces, DrawElementsType.UnsignedInt, new IntPtr(cache.terrain[filename].renderBatches[i].firstFace * 4));
                    }
                }
            }

            var error = GL.GetError().ToString();

            if (error != "NoError")
            {
                Console.WriteLine(error);
            }

            GL.BindVertexArray(0);
            renderCanvas.SwapBuffers();
        }

        public void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            Update();
            renderCanvas.Invalidate();
        }
    }
}
