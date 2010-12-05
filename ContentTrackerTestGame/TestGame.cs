using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Microsoft.Xna.Framework.Net;
using Microsoft.Xna.Framework.Storage;
using Arands.Content;

namespace ContentTrackerTestGame
{


    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class TestGame : Microsoft.Xna.Framework.Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        SpriteFont spriteFont;
        ContentTracker contentTracker;

        // Are we currently using asynchronous loading?
        bool asyncLoad = true;

        // Some hard coded info for setting up the scene
        ModelInfo skyModel;
        WorldChunk[] chunks;
        string modelNameTerrain = "Content\\TerrainDarkStone2";
        string[] chunkModelNames = new string[] {  "Pyramid" , "Pyramid", "Teapot",
                                                   "Pyramid", "Pyramid", "Cylinder",
                                                   "Pyramid", "Torus", "Cone", };
        int chunkCountX = 3;
        int chunkCountZ = 3;

        // For demo purposes these have been made static
        public static Vector3 FogColor = new Vector3(0.572f, 0.647f, 0.722f);
        public static Vector3 SpecColor = Vector3.Zero;

        // Far clip distance. Whether a WorldChunk should be loaded or not is based on this value
        public static float FarClip = 900.0f;

        // Camera state
        Vector3 cameraPos;
        float mouseRotY = 0.0f;
        float mouseRotX = 0.0f;
        Matrix viewMatrix;
        Matrix camWorldMatrix;
        Matrix projMatrix; 

        // Input states
        KeyboardState keyState;
        KeyboardState keyStateLast;
        MouseState mouseState;
        MouseState mouseStateLast;
        GamePadState padState;
        GamePadState padStateLast;

        public TestGame()
        {
            graphics = new GraphicsDeviceManager(this);
            contentTracker = new ContentTracker(this.Services);
            this.Content = contentTracker;
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            spriteFont = contentTracker.Load<SpriteFont>("Content\\SpriteFont1");

            // Load sky model. 
            // If using async load, you need to make sure it is loaded 
            // before using it (see the Draw method).
            // Thanks to LuckyWolf19 for finding this bug
            skyModel = new ModelInfo();
            skyModel.ModelName = "Content\\SkyDay";
            skyModel.FogEnable = false;
            skyModel.LoadContent(contentTracker, true);

            // The chunks are planes 1000 by 1000 units.
            float chunkSize = 1000.0f;
            float chunkRadius = (float)Math.Sqrt(chunkSize * chunkSize * 2.0f);

            // Calculate chunk positions
            int chunkCount = chunkCountX * chunkCountZ;
            chunks = new WorldChunk[chunkCount];
            for (int i = 0; i < chunkCountX; i++)
            {
                for (int j = 0; j < chunkCountZ; j++)
                {
                    // spread chunks in the XZ plane
                    Vector3 chunkPos = new Vector3(
                                                (i - chunkCountX / 2.0f) * chunkSize, 
                                                0.0f,
                                                (j - chunkCountZ / 2.0f) * chunkSize);

                    int chunkID = i * chunkCountZ + j;

                    // Create the chunk. Currently they only have two models each - the terrain and one other
                    chunks[chunkID] = new WorldChunk(
                                        Matrix.CreateTranslation(chunkPos),
                                        new string[] { modelNameTerrain , "Content\\" + chunkModelNames[chunkID] });

                    // Create bounding sphere for entire chunk
                    chunks[chunkID].Bounds = new BoundingSphere(chunkPos, chunkRadius);
                }
            }
            
            // Initialise the camera
            cameraPos = Vector3.UnitY * 100.0f;

            projMatrix = Matrix.CreatePerspectiveFieldOfView(
                                            (float)MathHelper.PiOver4,
                                            graphics.GraphicsDevice.Viewport.AspectRatio,
                                            1.0f, FarClip);

            mouseRotX = 0.0f;
            mouseRotY = 0.0f;
            mouseState = Mouse.GetState(); // Ensures we are initially facing in a sensible direction

            
        }


        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
            contentTracker.Unload();
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            // Allows the game to exit
            if (padState.Buttons.Back == ButtonState.Pressed)
                this.Exit();

            // Update input states
            keyStateLast = keyState;
            keyState = Keyboard.GetState();
            mouseStateLast = mouseState;
            mouseState = Mouse.GetState();
            padStateLast = padState;
            padState = GamePad.GetState(PlayerIndex.One);

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Camera controls (use mouse, arrow keys and shift)
            float speed = 200.0f;

            if (keyState.IsKeyDown(Keys.RightShift) ||
                padState.Buttons.B == ButtonState.Pressed)
            {
                speed *= 4.0f;
            }

            if (keyState.IsKeyDown(Keys.Left) ||
                padState.DPad.Left == ButtonState.Pressed)
            {
                cameraPos += speed * dt * camWorldMatrix.Left;
            }
            else if (keyState.IsKeyDown(Keys.Right) ||
                     padState.DPad.Right == ButtonState.Pressed)
            {
                cameraPos -= speed * dt * camWorldMatrix.Left;
            }

            Vector3 forward = Vector3.Normalize(
                                new Vector3(camWorldMatrix.Forward.X,
                                            0.0f,
                                            camWorldMatrix.Forward.Z));

            if (keyState.IsKeyDown(Keys.Up) ||
                padState.DPad.Up == ButtonState.Pressed)
            {
                cameraPos += speed * dt * forward;
            }
            else if (keyState.IsKeyDown(Keys.Down) ||
                     padState.DPad.Down == ButtonState.Pressed)
            {
                cameraPos -= speed * dt * forward;
            }

            // Add mouse and gamepad rotations to support both
            mouseRotY += 0.01f * (float)(mouseStateLast.X - mouseState.X);
            mouseRotY -= 0.05f * padStateLast.ThumbSticks.Right.X;
            mouseRotX += 0.01f * (float)(mouseStateLast.Y - mouseState.Y);
            mouseRotX += 0.05f * padStateLast.ThumbSticks.Right.Y;
            mouseRotX = MathHelper.Clamp(mouseRotX, -MathHelper.PiOver2 + 0.0001f, MathHelper.PiOver2 - 0.0001f);

            camWorldMatrix = Matrix.CreateFromYawPitchRoll(mouseRotY, mouseRotX, 0.0f) * 
                             Matrix.CreateTranslation(cameraPos);

            viewMatrix = Matrix.Invert(camWorldMatrix);



            // Toggle Async load
            if ((keyState.IsKeyDown(Keys.A) && !keyStateLast.IsKeyDown(Keys.A)) ||
                (padState.Buttons.A == ButtonState.Pressed && padStateLast.Buttons.A == ButtonState.Released))
            {
                asyncLoad = !asyncLoad;
            }

            //
            // Determine chunk visibility and load or release as necessary.
            //

            // For testing, press space bar to release all active chunks
            if (keyState.IsKeyDown(Keys.Space) && !keyStateLast.IsKeyDown(Keys.Space))
            {
                for (int i = 0; i < chunkCountX * chunkCountZ; i++)
                {
                    if (chunks[i].Active)
                        chunks[i].ReleaseContent();
                }
            }

            // Don't test using the camera's view frustrum. Instead base "shouldBeLoaded"
            // off the distance from camera. Then loading/releasing won't occur if player turns around
            int chunkCount = chunkCountX * chunkCountZ;
            for (int i = 0; i < chunkCount; i++)
            {                
                Vector3 tmp = Vector3.Subtract(chunks[i].Bounds.Center, cameraPos);
                float distToChunk = tmp.Length() - chunks[i].Bounds.Radius;
                bool shouldBeLoaded = distToChunk < FarClip;

                if (shouldBeLoaded && !chunks[i].Active)
                {
                    // Load the chunk
                    chunks[i].LoadContent(contentTracker, asyncLoad);
                }
                else if (!shouldBeLoaded && chunks[i].Active)
                {
                    // Unload the chunk
                    chunks[i].ReleaseContent();
                }
            }


            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            graphics.GraphicsDevice.Clear(new Color(FogColor));
            graphics.GraphicsDevice.BlendState = BlendState.Opaque;
            graphics.GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            graphics.GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

            // Draw Sky
            if (skyModel.Tracker.Status == AssetStatus.Active)
            {
                foreach (ModelMesh mm in skyModel.Model.Meshes)
                {
                    foreach (BasicEffect be in mm.Effects)
                    {
                        // Sky model has radius 100 units
                        // Scale it based on the far clip plane
                        // Also move it around with the camera
                        be.World = Matrix.CreateScale(FarClip / 100.0f) *
                                   Matrix.CreateTranslation(
                                            new Vector3(cameraPos.X,
                                                        cameraPos.Y - 100.0f,
                                                        cameraPos.Z));
                        be.View = viewMatrix;
                        be.Projection = projMatrix;

                        be.LightingEnabled = false;
                    }

                    mm.Draw();
                }
            }

            // Draw world chunks. They will check their active status, both 
            // at a chunk level and for each model.
            int chunkCount = chunkCountX * chunkCountZ;
            for (int i = 0; i < chunkCount; i++)
            {
                chunks[i].Draw(ref viewMatrix, ref projMatrix);
            }

            // Draw stats & instructions
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

            float ypos = 20.0f;
            float xpos = 20.0f;

            spriteBatch.DrawString(spriteFont, string.Format("Load Async (A): {0}", asyncLoad), new Vector2(xpos, ypos), Color.DarkKhaki);
            ypos += 14.0f;

            spriteBatch.DrawString(spriteFont, string.Format("Loading: {0}", contentTracker.IsLoading ? true : false), new Vector2(xpos, ypos), Color.DarkKhaki);
            ypos += 18.0f;

            List<string> assetNames = contentTracker.GetLoadedAssetNames();

            for (int i = 0; i < assetNames.Count; i++)
            {
                // Stop if we get off the screen
                if (ypos > graphics.GraphicsDevice.Viewport.Height)
                    break;

                // At root level, only show content with no parent references
                // This gives us all the assets requested by the user
                AssetTracker tracker = contentTracker.GetTracker(assetNames[i]);
                if (tracker.ReferredToBy.Count > 0)
                    continue;

                spriteBatch.DrawString( spriteFont, 
                                        string.Format("{0} ({1})", assetNames[i], tracker.RefCount), 
                                        new Vector2(xpos, ypos), Color.White);
                ypos += 14.0f;

                // Now show all children indented.
                // Note: this only handles displaying a single level hierarchy, 
                // ie Model->(Texture/Effect)
                for (int j = 0; j < tracker.RefersTo.Count; j++)
                {
                    spriteBatch.DrawString(spriteFont,
                        string.Format("{0} ({1})", tracker.RefersTo[j], contentTracker.GetReferenceCount(tracker.RefersTo[j])),
                        new Vector2(xpos + 30.0f, ypos), Color.White);
                    ypos += 14.0f;
                }
            }

            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
