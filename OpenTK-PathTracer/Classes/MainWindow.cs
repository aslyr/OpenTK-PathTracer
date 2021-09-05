﻿using System;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Collections.Generic;
using OpenTK;
using OpenTK.Input;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using OpenTK_PathTracer.Render;
using OpenTK_PathTracer.GameObjects;
using OpenTK_PathTracer.Render.Objects;

namespace OpenTK_PathTracer
{
    class MainWindow : GameWindow
    {
        public const int MAX_GAMEOBJECTS_SPHERES = 256, MAX_GAMEOBJECTS_CUBOIDS = 64;
        public const float EPSILON = 0.005f, FOV = 103;

        public MainWindow() : base(832, 832, new GraphicsMode(0, 0, 0, 0)) {  /*WindowState = WindowState.Maximized;*/  }


        public Matrix4 projection, inverseProjection;
        Vector2 nearFarPlane = new Vector2(EPSILON, 2000f);
        public int FPS, UPS;
        private int fps, ups;

        public readonly Camera Camera = new Camera(new Vector3(-9, 12, 4), Vector3.Normalize(new Vector3(0.3f, -0.3f, -0.5f)), new Vector3(0, 1, 0)); // new Vector3(-9, 12, 4) || new Vector3(-11, 9.3f, -9.7f)


        public bool IsRenderInBackground = true;
        protected override void OnRenderFrame(FrameEventArgs e)
        {
            if (Focused || IsRenderInBackground)
            {
                //AtmosphericScatterer.Run(Camera.Position);
                PathTracer.Run();
                
                //Rasterizer.Run((from c in grid.Cells select c.AABB).ToArray()); // new AABB[] { new AABB(Vector3.One, Vector3.One) } (from c in grid.Cells select c.AABB).ToArray()
                PostProcesser.Run(PathTracer.Result, Rasterizer.Result);

                Framebuffer.Clear(0, ClearBufferMask.ColorBufferBit);
                PostProcesser.Result.AttachToUnit(0);
                finalProgram.Use();
                GL.DrawArrays(PrimitiveType.Quads, 0, 4);

                if (Focused)
                {
                    Render.GUI.Final.Run(this, (float)e.Time, out bool frameChanged);
                    if (frameChanged)
                        PathTracer.ThisRenderNumFrame = 0;
                }
                SwapBuffers();
                fps++;
            }
            
            base.OnRenderFrame(e);
        }


        readonly Stopwatch fpsTimer = Stopwatch.StartNew();
        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            if (fpsTimer.ElapsedMilliseconds >= 1000)
            {
                Title = $"FPS: {fps}; RayDepth: {PathTracer.RayDepth}; UPS: {ups} Position {Camera.Position}";
                FPS = fps;
                UPS = ups;
                fps = 0;
                ups = 0;

                fpsTimer.Restart();
            }
            ThreadManager.InvokeQueuedActions();
            KeyboardManager.Update(Keyboard.GetState());
            MouseManager.Update(Mouse.GetState());

            if (Focused)
            {
                if (Render.GUI.Final.ImGuiIOPtr.WantCaptureMouse && !CursorVisible)
                {
                    Point _point = PointToScreen(new Point(Width / 2, Height / 2));
                    Mouse.SetPosition(_point.X, _point.Y);
                }

                //grid.Update(gameObjects);
                Render.GUI.Final.Update(this);

                if (KeyboardManager.IsKeyDown(Key.Escape))
                    Close();

                if (KeyboardManager.IsKeyTouched(Key.V))
                    VSync = VSync == VSyncMode.Off ? VSyncMode.On : VSyncMode.Off;

                if (KeyboardManager.IsKeyTouched(Key.F11))
                    WindowState = WindowState == WindowState.Normal ? WindowState.Fullscreen : WindowState.Normal;

                if (KeyboardManager.IsKeyTouched(Key.E) && !Render.GUI.Final.ImGuiIOPtr.WantCaptureKeyboard)
                {
                    CursorVisible = !CursorVisible;
                    CursorGrabbed = !CursorGrabbed;
                    
                    if (!CursorVisible)
                    {
                        MouseManager.Update(Mouse.GetState());
                        Camera.Velocity = Vector3.Zero;
                    }
                }

                if (!CursorVisible)
                {
                    Camera.ProcessInputs((float)args.Time, out bool frameChanged);
                    if (frameChanged)
                        PathTracer.ThisRenderNumFrame = 0;
                }

                int oldOffset = Vector4.SizeInBytes * 4 * 2 + Vector4.SizeInBytes;
                BasicDataUBO.Append(Vector4.SizeInBytes * 4 * 3, new Matrix4[] { Camera.View, Camera.View.Inverted(), Camera.View * projection });
                BasicDataUBO.Append(Vector4.SizeInBytes, Camera.Position);
                BasicDataUBO.Append(Vector4.SizeInBytes, Camera.ViewDir);
                BasicDataUBO.BufferOffset = oldOffset;
            }
            ups++;
            base.OnUpdateFrame(args);
        }

        public readonly List<GameObjectBase> GameObjects = new List<GameObjectBase>();
        ShaderProgram finalProgram;
        public BufferObject BasicDataUBO, GameObjectsUBO;
        public PathTracing PathTracer;
        public Rasterizer Rasterizer;
        public ScreenEffect PostProcesser;
        public AtmosphericScattering AtmosphericScatterer;
        Grid grid = new Grid(4, 4, 3);
        protected override void OnLoad(EventArgs e)
        {
            Console.WriteLine($"GPU: {GL.GetString(StringName.Renderer)}");
            Console.WriteLine($"OpenGL: {GL.GetString(StringName.Version)}");
            Console.WriteLine($"GLSL: {GL.GetString(StringName.ShadingLanguageVersion)}");
            // FIX: For some reason MaxUniformBlockSize seems to be ≈33728 for RX 5700XT, although GL.GetInteger(MaxUniformBlockSize) returns 572657868.
            // I dont want to use SSBO, because of performance. Also see: https://opengl.gpuinfo.org/displayreport.php?id=6204 
            Console.WriteLine($"MaxShaderStorageBlockSize: {GL.GetInteger((GetPName)All.MaxShaderStorageBlockSize)}");
            Console.WriteLine($"MaxUniformBlockSize: {GL.GetInteger(GetPName.MaxUniformBlockSize)}");

            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.Multisample);
            // TextureCubeMapSeamless gets enabled in Texture class through GL_ARB_seamless_cubemap_per_texture to be compatible with ARB_bindless_texture
            //GL.Enable(EnableCap.TextureCubeMapSeamless);

            VSync = VSyncMode.Off;
            CursorVisible = false;
            CursorGrabbed = true;

            //EnvironmentMap skyBox = new EnvironmentMap();
            //skyBox.SetAllFacesParallel(new string[]
            //{
            //    @"Src\Textures\EnvironmentMap\posx.png",
            //    @"Src\Textures\EnvironmentMap\negx.png",
            //    @"Src\Textures\EnvironmentMap\posy.png",
            //    @"Src\Textures\EnvironmentMap\negy.png",
            //    @"Src\Textures\EnvironmentMap\posz.png",
            //    @"Src\Textures\EnvironmentMap\negz.png",
            //});
            AtmosphericScatterer = new AtmosphericScattering(128, 100, 10, 2.1f, 35.0f, 0.01f, new Vector3(680, 550, 440), new Vector3(0, 500, 0));
            AtmosphericScatterer.Run();

            finalProgram = new ShaderProgram(new Shader(ShaderType.VertexShader, @"Src\Shaders\screenQuad.vs"), new Shader(ShaderType.FragmentShader, @"Src\Shaders\final.frag"));
            BasicDataUBO = new BufferObject(BufferRangeTarget.UniformBuffer, 0, Vector4.SizeInBytes * 4 * 5 + Vector4.SizeInBytes * 3, BufferUsageHint.StreamRead);
            GameObjectsUBO = new BufferObject(BufferRangeTarget.UniformBuffer, 1, Sphere.GPU_INSTANCE_SIZE * MAX_GAMEOBJECTS_SPHERES + Cuboid.GPU_INSTANCE_SIZE * MAX_GAMEOBJECTS_CUBOIDS, BufferUsageHint.StreamRead);
            UBOCompatibleBase.BufferObject = GameObjectsUBO;

            PathTracer = new PathTracing(new EnvironmentMap(AtmosphericScatterer.Result), Width, Height, 8, 1, 20f, 0.14f);
            //PathTracer = new PathTracing(skyBox, Width, Height, 8, 1, 20f, 0.07f);
            Rasterizer = new Rasterizer(Width, Height);
            PostProcesser = new ScreenEffect(new Shader(ShaderType.FragmentShader, @"Src\Shaders\PostProcessing\fragment.frag"), Width, Height);
            float width = 40, height = 25, depth = 25;
            
            {
                int balls = 6;
                float radius = 1.3f;
                Vector3 dimensions = new Vector3(width * 0.6f, height, depth);
                for (float x = 0; x < balls; x++)
                {
                    for (float y = 0; y < balls; y++)
                    {
                        GameObjects.Add(new Sphere(new Vector3(dimensions.X / balls * x * 1.1f - dimensions.X / 2, (dimensions.Y / balls) * y - dimensions.Y / 2 + radius, -17), radius, PathTracer.NumSpheres++, new Material(albedo: new Vector3(0.59f, 0.59f, 0.99f), emissiv: new Vector3(0), refractionColor: Vector3.Zero, specularChance: x / (balls - 1), specularRoughness: y / (balls - 1), indexOfRefraction: 1f, refractionChance: 0.0f, refractionRoughnes: 0.1f)));
                    }
                }

                Vector3 delta = dimensions / balls;
                for (float x = 0; x < balls; x++)
                {
                    Material material = Material.Zero;
                    material.Albedo = new Vector3(0.9f, 0.25f, 0.25f);
                    material.SpecularChance = 0.02f;
                    material.IOR = 1.1f;
                    material.RefractionChance = 0.98f;
                    material.RefractionColor = new Vector3(1, 2, 3) * (x / balls);
                    Vector3 position = new Vector3(-dimensions.X / 2 + radius + delta.X * x, 0f, -5f);
                    GameObjects.Add(new Sphere(position, radius, PathTracer.NumSpheres++, material));


                    Material material1 = Material.Zero;
                    material1.SpecularChance = 0.02f;
                    material1.SpecularRoughness = (x / balls);
                    material1.IOR = 1.1f;
                    material1.RefractionChance = 0.98f;
                    material1.RefractionRoughnes = x / balls;
                    material1.RefractionColor = Vector3.Zero;
                    position = new Vector3(-dimensions.X / 2 + radius + delta.X * x, -10f, -5f);
                    GameObjects.Add(new Sphere(position, radius, PathTracer.NumSpheres++, material1));
                }
            }

            {
                Cuboid down = new Cuboid(new Vector3(0, -height / 2, -10), new Vector3(width, EPSILON, depth), PathTracer.NumCuboids++, new Material(albedo: new Vector3(1, 0.4f, 0.04f), emissiv: new Vector3(0), refractionColor: Vector3.Zero, specularChance: 0.11f, specularRoughness: 0.051f, indexOfRefraction: 1f, refractionChance: 0, refractionRoughnes: 0));

                Cuboid up = new Cuboid(new Vector3(down.Position.X, down.Position.Y + height, down.Position.Z), new Vector3(down.Dimensions.X, EPSILON, down.Dimensions.Z), PathTracer.NumCuboids++, new Material(albedo: new Vector3(0.6f), emissiv: new Vector3(0), refractionColor: Vector3.Zero, specularChance: 0.023f, specularRoughness: 0.051f, indexOfRefraction: 1f, refractionChance: 0, refractionRoughnes: 0));
                Cuboid upLight0 = new Cuboid(new Vector3(up.Position.X, down.Position.Y + height - EPSILON, down.Position.Z), new Vector3(down.Dimensions.X / 3, EPSILON, down.Dimensions.Z / 3), PathTracer.NumCuboids++, new Material(albedo: new Vector3(0.04f), emissiv: new Vector3(0.917f, 0.945f, 0.513f) * 1.5f, refractionColor: Vector3.Zero, specularChance: 0.0f, specularRoughness: 1f, indexOfRefraction: 1f, refractionChance: 0, refractionRoughnes: 0));

                Cuboid back = new Cuboid(new Vector3(down.Position.X, down.Position.Y + height / 2, down.Position.Z - depth / 2), new Vector3(width, height, EPSILON), PathTracer.NumCuboids++, new Material(albedo: new Vector3(0.6f), emissiv: new Vector3(0), refractionColor: Vector3.Zero, specularChance: 0.0f, specularRoughness: 0f, indexOfRefraction: 1f, refractionChance: 0, refractionRoughnes: 0));
                //Cuboid front = new Cuboid(new Vector3(down.Position.X, down.Position.Y + height / 2 + Epsilon, down.Position.Z + depth / 2 - 0.3f / 2), new Vector3(width, height - Epsilon * 2, 0.3f), instancesCuboids++, new Material(albedo: new Vector3(1f), emissiv: new Vector3(0), refractionColor: new Vector3(0.01f), specularChance: 0.04f, specularRoughness: 0f, indexOfRefraction: 1f, refractionChance: 0.954f, refractionRoughnes: 0));

                Cuboid right = new Cuboid(new Vector3(down.Position.X + width / 2, down.Position.Y + height / 2, down.Position.Z), new Vector3(EPSILON, height, depth), PathTracer.NumCuboids++, new Material(albedo: new Vector3(1, 0.4f, 0.4f), emissiv: new Vector3(0), refractionColor: Vector3.Zero, specularChance: 0.5f, specularRoughness: 0, indexOfRefraction: 1f, refractionChance: 0, refractionRoughnes: 0));
                Cuboid left = new Cuboid(new Vector3(down.Position.X - width / 2, down.Position.Y + height / 2, down.Position.Z), new Vector3(EPSILON, height, depth), PathTracer.NumCuboids++, new Material(albedo: new Vector3(0.24f, 0.6f, 0.24f), emissiv: new Vector3(0), refractionColor: Vector3.Zero, specularChance: 0.0f, specularRoughness: 0f, indexOfRefraction: 1f, refractionChance: 0, refractionRoughnes: 0));

                Cuboid middle = new Cuboid(new Vector3(right.Position.X * 0.55f, down.Position.Y + 12f / 2 + EPSILON, back.Position.Z * 0.4f), new Vector3(3f, 12, 3f), PathTracer.NumCuboids++, new Material(albedo: new Vector3(0.917f, 0.945f, 0.513f), emissiv: new Vector3(0), refractionColor: Vector3.Zero, specularChance: 1.0f, specularRoughness: 0f, indexOfRefraction: 1f, refractionChance: 0, refractionRoughnes: 0));

                GameObjects.AddRange(new Cuboid[] { down, upLight0, up, back, right, left, middle });
            }
            
            for (int i = 0; i < GameObjects.Count; i++)
                GameObjects[i].Upload();

            {
                //int ssboCellsSize = 8 * 8 * 8;
                //GridCellsSSBO = new BufferObject(BufferRangeTarget.ShaderStorageBuffer, 0, ssboCellsSize * Grid.Cell.GPUInstanceSize + 1000 * sizeof(int), BufferUsageHint.StreamRead);

                grid.Update(GameObjects);

                //Vector4[] gridGPUData = grid.GetGPUFriendlyGridData();
                //GridCellsSSBO.SubData(0, Vector4.SizeInBytes * gridGPUData.Length, gridGPUData);

                //int[] indecisData = grid.GetGPUFriendlyIndecisData();
                //GridCellsSSBO.SubData(ssboCellsSize * Grid.Cell.GPUInstanceSize, indecisData.Length * sizeof(int), indecisData);
                //PathTracing.program.Upload("ssboCellsSize", grid.Cells.Count);
            }
        }

        int lastWidth = -1, lastHeight = -1;
        protected override void OnResize(EventArgs e)
        {
            if (lastWidth != Width && lastHeight != Height && Width != 0 && Height != 0) // dont resize when minimizing and maximizing
            {
                PathTracer.SetSize(Width, Height);
                Rasterizer.SetSize(Width, Height);
                PostProcesser.SetSize(Width, Height);
                Render.GUI.Final.Resize(Width, Height);

                projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(FOV), Width / (float)Height, nearFarPlane.X, nearFarPlane.Y);
                inverseProjection = projection.Inverted();
                BasicDataUBO.BufferOffset = 0;
                BasicDataUBO.Append(Vector4.SizeInBytes * 4 * 2, new Matrix4[] { projection, inverseProjection });
                BasicDataUBO.Append(Vector4.SizeInBytes, nearFarPlane);
                lastWidth = Width; lastHeight = Height;
            }
            base.OnResize(e);
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            Render.GUI.Final.ImGuiController.PressChar(e.KeyChar);
        }

        protected override void OnFocusedChanged(EventArgs e)
        {
            if (Focused)
                MouseManager.Update(Mouse.GetState());
        }

        protected override void OnClosed(EventArgs e)
        {
            ImGuiNET.ImGui.SaveIniSettingsToDisk("imgui.ini");
            base.OnClosed(e);
        }

        public bool RayTrace(Ray ray, out GameObjectBase gameObject, out float t1, out float t2)
        {
            t1 = t2 = 0;
            gameObject = null;
            float tMin = float.MaxValue;
            for (int i = 0; i < GameObjects.Count; i++)
            {
                if (GameObjects[i].IntersectsRay(ray, out float tempT1, out float tempT2) && tempT2 > 0 && tempT1 < tMin)
                {
                    t1 = tempT1; t2 = tempT2;
                    tMin = GetSmallestPositive(t1, t2);
                    gameObject = GameObjects[i];
                }
            }

            return tMin != float.MaxValue;
        }
        public bool RayTrace(Grid grid, Ray ray, out GameObjectBase gameObject)
        {
            gameObject = null;
            float tMin = float.MaxValue, cellMin = float.MaxValue;
            float t1, t2;

            //TODO: Use DDA algorithm for traversal
            for (int i = 0; i < grid.Cells.Count; i++)
            {
                if (ray.IntersectsAABB(grid.Cells[i].AABB, out float cellT1, out float cellT2) && cellT2 > 0 && cellT1 <= cellMin)
                {
                    for (int j = grid.Cells[i].Start; j < grid.Cells[i].End; j++)
                    {
                        if (GameObjects[grid.Indecis[j]].IntersectsRay(ray, out t1, out t2) && t1 <= cellT2 && t2 > 0 && t1 < tMin)
                        {
                            gameObject = GameObjects[grid.Indecis[j]];
                            tMin = GetSmallestPositive(t1, t2);
                            cellMin = cellT1;
                        }
                    }
                }
            }

            return tMin != float.MaxValue;
        }
        public static float GetSmallestPositive(float t1, float t2) => t1 < 0 ? t2 : t1;
        
        public void NewRandomBalls(float xRange, float yRange, float zRange)
        {
            int balls = 6;
            float radius = 1.3f;
            Vector3 dimensions = new Vector3(xRange * 0.6f, yRange, zRange);

            int instance = 0;
            for (float x = 0; x < balls; x++)
            {
                for (float y = 0; y < balls; y++)
                {
                    GameObjects[instance] = new Sphere(new Vector3(dimensions.X / balls * x * 1.1f - dimensions.X / 2, (dimensions.Y / balls) * y - dimensions.Y / 2 + radius, -17), radius, instance, Material.GetRndMaterial());
                    GameObjects[instance++].Upload();
                }
            }
        }
    }
}