﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using ImGuiNET;
using UAlbion.Core.Events;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using UAlbion.Core.Objects;
using UAlbion.Core.Textures;

namespace UAlbion.Core
{
    public class Engine : Component, IDisposable
    {
        static readonly IList<Handler> Handlers = new Handler[]
        {
            new Handler<Engine, ToggleFullscreenEvent>((x, _) => x.ToggleFullscreenState()),
            new Handler<Engine, LoadRenderDocEvent>((x, _) =>
            {
                if (_renderDoc == null && RenderDoc.Load(out _renderDoc))
                    x.ChangeBackend(x.GraphicsDevice.BackendType, true);
            }),
            new Handler<Engine, ToggleResizableEvent>((x, _) => x.Window.Resizable = !x.Window.Resizable),
            new Handler<Engine, ToggleVisibleBorderEvent>((x, _) => x.Window.BorderVisible = !x.Window.BorderVisible),
            new Handler<Engine, QuitEvent>((x, e) => x.Window.Close())
        };

        static RenderDoc _renderDoc;

        public ITextureManager TextureManager { get; }
        readonly FrameTimeAverager _frameTimeAverager = new FrameTimeAverager(0.5);
        readonly FullScreenQuad _fullScreenQuad;
        readonly ScreenDuplicator _duplicator;
        readonly DebugGuiRenderer _igRenderable;
        readonly SceneContext _sceneContext = new SceneContext();
        readonly DebugMenus _debugMenus;

        Scene _scene;
        CommandList _frameCommands;
        TextureSampleCount? _newSampleCount;
        bool _windowResized;
        bool _recreateWindow = true;

        internal GraphicsDevice GraphicsDevice { get; private set; }
        internal Sdl2Window Window { get; private set; }
        internal RenderDoc RenderDoc => _renderDoc;
        internal string FrameTimeText => _frameTimeAverager.CurrentAverageFramesPerSecond.ToString("000.0 fps / ") + _frameTimeAverager.CurrentAverageFrameTimeMilliseconds.ToString("#00.00 ms");

        // TODO: Build scenes from config
        public Scene Create2DScene()
        {
            var camera = new OrthographicCamera(Window);
            var scene = new Scene(camera);
            scene.AddRenderer(_igRenderable);
            scene.AddRenderer(_duplicator);
            scene.AddRenderer(_fullScreenQuad);

            scene.AddComponent(this);
            scene.AddComponent(camera);
            scene.AddComponent(_igRenderable);
            scene.AddComponent(_duplicator);
            scene.AddComponent(_fullScreenQuad);
            scene.AddComponent(_debugMenus);
            return scene;
        }

        public Scene Create3DScene()
        {
            var camera = new PerspectiveCamera(GraphicsDevice, Window);
            var scene = new Scene(camera);
            scene.AddRenderer(_igRenderable);
            scene.AddRenderer(_duplicator);
            scene.AddRenderer(_fullScreenQuad);

            scene.AddComponent(this);
            scene.AddComponent(camera);
            scene.AddComponent(_igRenderable);
            scene.AddComponent(_duplicator);
            scene.AddComponent(_fullScreenQuad);
            scene.AddComponent(_debugMenus);
            return scene;
        }

        public void SetScene(Scene scene)
        {
            _scene = scene;
            _sceneContext.SetCurrentScene(_scene);
            CreateAllObjects();
            ImGui.StyleColorsClassic();
        }

        public Engine(GraphicsBackend backend) : base(Handlers)
        {
            TextureManager = new TextureManager();
            ChangeBackend(backend);

            _igRenderable = new DebugGuiRenderer(Window.Width, Window.Height);
            _duplicator = new ScreenDuplicator();
            _fullScreenQuad = new FullScreenQuad();
            _debugMenus = new DebugMenus(this);

            Sdl2Native.SDL_Init(SDLInitFlags.GameController);
        }

        public void Run()
        {
            if (_scene == null)
                throw new InvalidOperationException("The scene must be set before the main loop can be run.");

            var frameCounter = new FrameCounter();
            while (Window.Exists)//*/ && frameCounter.FrameCount < 20)
            {
                double deltaSeconds = frameCounter.StartFrame();
                Raise(new BeginFrameEvent());
                Sdl2Events.ProcessEvents();
                InputSnapshot snapshot = Window.PumpEvents();
                Raise(new InputEvent(deltaSeconds, snapshot, Window.MouseDelta));
                Update((float)deltaSeconds);
                if (!Window.Exists)
                    break;

                Draw();
            }

            DestroyAllObjects();
            GraphicsDevice.Dispose();
        }

        void Update(float deltaSeconds)
        {
            _frameTimeAverager.AddTime(deltaSeconds);
            Raise(new EngineUpdateEvent(deltaSeconds));
        }

        internal void ChangeMsaa(int msaaOption)
        {
            _newSampleCount = (TextureSampleCount)msaaOption;
        }

        internal void RefreshDeviceObjects(int numTimes)
        {
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < numTimes; i++)
            {
                DestroyAllObjects();
                CreateAllObjects();
            }
            sw.Stop();
            Console.WriteLine($"Refreshing resources {numTimes} times took {sw.Elapsed.TotalSeconds} seconds.");
        }

        void ToggleFullscreenState()
        {
            bool isFullscreen = Window.WindowState == WindowState.BorderlessFullScreen;
            Window.WindowState = isFullscreen ? WindowState.Normal : WindowState.BorderlessFullScreen;
        }

        void Draw()
        {
            Debug.Assert(Window.Exists);
            int width = Window.Width;
            int height = Window.Height;

            CoreTrace.Log.Info("Engine", "Start draw");
            if (_windowResized)
            {
                _windowResized = false;

                GraphicsDevice.ResizeMainWindow((uint)width, (uint)height);
                Raise(new WindowResizedEvent(width, height));
                CommandList cl = GraphicsDevice.ResourceFactory.CreateCommandList();
                cl.Begin();
                _sceneContext.RecreateWindowSizedResources(GraphicsDevice, cl);
                cl.End();
                GraphicsDevice.SubmitCommands(cl);
                cl.Dispose();
                CoreTrace.Log.Info("Engine", "Resize finished");
            }

            if (_newSampleCount != null)
            {
                _sceneContext.MainSceneSampleCount = _newSampleCount.Value;
                _newSampleCount = null;
                DestroyAllObjects();
                CreateAllObjects();
            }

            _frameCommands.Begin();
            _scene.RenderAllStages(GraphicsDevice, _frameCommands, _sceneContext);
            CoreTrace.Log.Info("Engine", "Swapping buffers...");
            GraphicsDevice.SwapBuffers();
            CoreTrace.Log.Info("Engine", "Draw complete");
        }

        internal void ChangeBackend(GraphicsBackend backend) => ChangeBackend(backend, false);

        internal void ChangeBackend(GraphicsBackend backend, bool forceRecreateWindow)
        {
            if (GraphicsDevice != null)
            {
                DestroyAllObjects();
                GraphicsDevice.Dispose();
            }

            if (Window == null || _recreateWindow || forceRecreateWindow)
            {
                Window?.Close();

                WindowCreateInfo windowInfo = new WindowCreateInfo
                {
                    X = Window?.X ?? 684,
                    Y = Window?.Y ?? 456,
                    WindowWidth = Window?.Width ?? 684,
                    WindowHeight = Window?.Height ?? 456,
                    WindowInitialState = Window?.WindowState ?? WindowState.Normal,
                    WindowTitle = "UAlbion"
                };

                Window = VeldridStartup.CreateWindow(ref windowInfo);
                Window.BorderVisible = false;
                Window.Resized += () => _windowResized = true;
            }

            GraphicsDeviceOptions gdOptions = new GraphicsDeviceOptions(false, null, false,
                ResourceBindingModel.Improved, true, true, false, true)
            {
                Debug = true ,
                SyncToVerticalBlank = true
            };

            GraphicsDevice = VeldridStartup.CreateGraphicsDevice(Window, gdOptions, backend);
            Window.Title = GraphicsDevice.BackendType.ToString();

            if (_scene != null)
            {
                _scene.Camera.UpdateBackend(GraphicsDevice);
                CreateAllObjects();
            }
        }

        void CreateAllObjects()
        {
            _frameCommands = GraphicsDevice.ResourceFactory.CreateCommandList();
            _frameCommands.Name = "Frame Commands List";

            CommandList initCL = GraphicsDevice.ResourceFactory.CreateCommandList();
            initCL.Name = "Recreation Initialization Command List";
            initCL.Begin();
            _sceneContext.CreateDeviceObjects(GraphicsDevice, initCL, _sceneContext);
            _scene.CreateAllDeviceObjects(GraphicsDevice, initCL, _sceneContext);
            initCL.End();
            GraphicsDevice.SubmitCommands(initCL);
            initCL.Dispose();
        }

        void DestroyAllObjects()
        {
            GraphicsDevice.WaitForIdle();
            _frameCommands.Dispose();
            _sceneContext.DestroyDeviceObjects();
            _scene.DestroyAllDeviceObjects();
            StaticResourceCache.DestroyAllDeviceObjects();
            TextureManager.DestroyDeviceObjects();
            GraphicsDevice.WaitForIdle();
        }

        public void Dispose()
        {
            _igRenderable?.Dispose();
            _frameCommands?.Dispose();
            _fullScreenQuad?.Dispose();
            //_graphicsDevice?.Dispose();
        }
    }
}
