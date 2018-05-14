﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace JoysOfEfficiency
{

    using Player = StardewValley.Farmer;
    using SVObject = StardewValley.Object;
    public class ModEntry : Mod
    {

        private Config config = null;
        private string hoverText;
        private bool catchingTreasure = false;
        private bool caughtFish = false;
        private int fishIndex = -1;

        public override void Entry(IModHelper helper)
        {
            config = helper.ReadConfig<Config>();
            GameEvents.UpdateTick += OnGameUpdate;
            InputEvents.ButtonPressed += OnButtonPressed;

            SaveEvents.BeforeSave += OnBeforeSave;

            SaveEvents.AfterLoad += OnPostSave;
            TimeEvents.AfterDayStarted += OnPostSave;

            GraphicsEvents.OnPostRenderHudEvent += OnPostRenderHUD;
        }

        #region EventHandlers

        private void OnGameUpdate(object sender, EventArgs args)
        {
            if (!Context.IsWorldReady)
            {
                return;
            }
            Player player = Game1.player;
            IReflectionHelper reflection = Helper.Reflection;
            if (config.AutoFishing && Game1.activeClickableMenu != null && Game1.activeClickableMenu is BobberBar bar)
            {
                AutoFishing(bar);
            }
            if (config.AutoWaterNearbyCrops && player.currentLocation.IsFarm)
            {
                RectangleE bb = Expand(player.GetBoundingBox(), 3 * Game1.tileSize);
                WateringCan can = null;
                foreach (Item item in player.Items)
                {
                    //Search Watering Can To Use
                    can = item as WateringCan;
                    if (can != null)
                    {
                        break;
                    }
                }
                if (can == null)
                {
                    return;
                }
                bool watered = false;
                foreach (KeyValuePair<Vector2, TerrainFeature> kv in player.currentLocation.terrainFeatures)
                {
                    Vector2 location = kv.Key;
                    TerrainFeature tf = kv.Value;
                    Point centre = tf.getBoundingBox(location).Center;
                    if (bb.IsInternalPoint(centre.X, centre.Y) && tf is HoeDirt dirt)
                    {
                        if (dirt.crop != null && dirt.state == 0 && player.Stamina >= 2 && can.WaterLeft > 0)
                        {
                            dirt.state = 1;
                            player.Stamina -= 2;
                            can.WaterLeft--;
                            watered = true;
                        }
                    }
                }
                if (watered)
                {
                    Game1.playSound("slosh");
                }
            }
            if (config.GiftInformation)
            {
                hoverText = null;
                if (player.CurrentTool != null || player.CurrentItem == null || (player.CurrentItem is SVObject && (player.CurrentItem as SVObject).bigCraftable))
                {
                    //Rejects tools, nothing, and bigCraftable objects(chests, machines, statues etc.)
                }
                else
                {
                    List<NPC> npcList = player.currentLocation.characters.Where(a => a.isVillager()).ToList();
                    foreach (NPC npc in npcList)
                    {
                        RectangleE npcRect = new RectangleE(npc.position.X, npc.position.Y - npc.sprite.getHeight() - Game1.tileSize / 1.5f, npc.sprite.getWidth() * 3 + npc.sprite.getWidth() / 1.5f, (npc.sprite.getHeight() * 3.5f));

                        if (npcRect.IsInternalPoint(Game1.getMouseX() + Game1.viewport.X, Game1.getMouseY() + Game1.viewport.Y))
                        {
                            //Mouse hovered on the NPC
                            StringBuilder key = new StringBuilder("taste.");
                            switch (npc.getGiftTasteForThisItem(player.CurrentItem))
                            {
                                case 0: key.Append("love."); break;
                                case 2: key.Append("like."); break;
                                case 4: key.Append("dislike."); break;
                                case 6: key.Append("hate."); break;
                                default: key.Append("neutral."); break;
                            }
                            switch (npc.gender)
                            {
                                case 0: key.Append("male"); break;
                                default: key.Append("female"); break;
                            }
                            Translation translation = Helper.Translation.Get(key.ToString());
                            hoverText = translation?.ToString();
                        }
                    }
                }
            }
            if (config.AutoPetNearbyAnimals)
            {
                int radius = 3 * Game1.tileSize;
                RectangleE bb = new RectangleE(player.position.X - radius, player.position.Y - radius, radius * 2, radius * 2);
                List<FarmAnimal> animalList = GetAnimalsList(player);
                foreach (FarmAnimal animal in animalList)
                {
                    if (bb.IsInternalPoint(animal.position.X, animal.position.Y) && !animal.wasPet)
                    {
                        animal.pet(player);
                    }
                }
            }
            if (player.CurrentTool is FishingRod rod)
            {
                IReflectedField<int> whichFish = reflection.GetField<int>(rod, "whichFish");
                if (rod.isNibbling && !rod.isReeling && !rod.hit && !rod.pullingOutOfWater && !rod.fishCaught)
                {
                    if (config.AutoFishing)
                    {
                        rod.DoFunction(player.currentLocation, 1, 1, 1, player);
                        rod.hit = true;
                    }
                }
                if(!rod.inUse())
                {
                    caughtFish = false;
                }
                if (config.MuchFasterBiting)
                {
                    rod.timeUntilFishingBite -= 1000;
                }
            }
        }

        private void OnButtonPressed(object sender, EventArgsInput args)
        {
            if (!Context.IsWorldReady || !Context.IsMainPlayer)
            {
                return;
            }
            IReflectionHelper reflection = Helper.Reflection;
            if (config.HowManyStonesLeft && args.Button == config.KeyShowStonesLeft)
            {
                LetAnimalsInHome();
                Player player = Game1.player;
                if(player.currentLocation is MineShaft mine)
                {
                    int stonesLeft = reflection.GetField<int>(mine, "stonesLeftOnThisLevel").GetValue();
                    if (stonesLeft == 0)
                    {
                        ShowHUDMessage("There are no stones in this level.");
                    }
                    else
                    {
                        bool single = stonesLeft == 1;
                        ShowHUDMessage(Format("There {0} {1} stone{2} left.", (single ? "is" : "are"), stonesLeft, (single ? "" : "s")));
                    }
                }
            }
        }

        private void OnPostRenderHUD(object sender, EventArgs args)
        {
            if(Context.IsPlayerFree && !string.IsNullOrEmpty(hoverText) && Game1.player.CurrentItem != null)
            {
                DrawSimpleTextbox(Game1.spriteBatch, hoverText, Game1.smallFont, Game1.player.CurrentItem);
            }
        }

        private void OnBeforeSave(object sender, EventArgs args)
        {
            if(!Context.IsWorldReady || !Context.IsPlayerFree || !config.AutoAnimalDoor)
            {
                return;
            }
            Log("BeforeSave");
            LetAnimalsInHome();

            Farm farm = Game1.getFarm();
            foreach (Building building in farm.buildings)
            {
                if (building is Coop coop)
                {
                    if (coop.indoors is AnimalHouse house)
                    {
                        if (house.animals.Any() && coop.animalDoorOpen)
                        {
                            coop.animalDoorOpen = false;
                            Helper.Reflection.GetField<int>(coop, "animalDoorMotion").SetValue(2);
                        }
                    }
                }
                else if (building is Barn barn)
                {
                    if (barn.indoors is AnimalHouse house)
                    {
                        if (house.animals.Any() && barn.animalDoorOpen)
                        {
                            barn.animalDoorOpen = false;
                            Helper.Reflection.GetField<int>(barn, "animalDoorMotion").SetValue(2);
                        }
                    }
                }
            }
        }

        private void OnPostSave(object sender, EventArgs args)
        {
            if (!Context.IsWorldReady || !Context.IsPlayerFree || !config.AutoAnimalDoor)
            {
                return;
            }
            Log("PostSave");
            if(Game1.isRaining || Game1.isSnowing)
            {
                Log("Don't open because of rainy/snowy weather.");
                return;
            }
            Farm farm = Game1.getFarm();
            foreach (Building building in farm.buildings)
            {
                if (building is Coop coop)
                {
                    if (coop.indoors is AnimalHouse house)
                    {
                        if (house.animals.Any() && !coop.animalDoorOpen)
                        {
                            coop.animalDoorOpen = true;
                            Helper.Reflection.GetField<int>(coop, "animalDoorMotion").SetValue(-2);
                        }
                    }
                }
                else if(building is Barn barn)
                {
                    if (barn.indoors is AnimalHouse house)
                    {
                        if (house.animals.Any() && !barn.animalDoorOpen)
                        {
                            barn.animalDoorOpen = true;
                            Helper.Reflection.GetField<int>(barn, "animalDoorMotion").SetValue(-3);
                        }
                    }
                }
            }
        }

        #endregion

        #region Utilities

        public void AutoFishing(BobberBar bar)
        {
            IReflectionHelper reflection = Helper.Reflection;

            IReflectedField<float> bobberSpeed = reflection.GetField<float>(bar, "bobberBarSpeed");

            float barPos = reflection.GetField<float>(bar, "bobberBarPos").GetValue();
            int barHeight = reflection.GetField<int>(bar, "bobberBarHeight").GetValue();
            float fishPos = reflection.GetField<float>(bar, "bobberPosition").GetValue();
            float treasurePos = reflection.GetField<float>(bar, "treasurePosition").GetValue();
            float distanceFromCatching = reflection.GetField<float>(bar, "distanceFromCatching").GetValue();
            bool treasureCaught = reflection.GetField<bool>(bar, "treasureCaught").GetValue();
            bool treasure = reflection.GetField<bool>(bar, "treasure").GetValue();
            float treasureApeearTimer = reflection.GetField<float>(bar, "treasureAppearTimer").GetValue();
            float bobberBarSpeed = bobberSpeed.GetValue();

            float up = barPos, down = barPos + barHeight;

            if(treasure && treasureApeearTimer <= 0 && !treasureCaught)
            {
                if(!catchingTreasure && distanceFromCatching > 0.7f)
                {
                    catchingTreasure = true;
                }
                if(catchingTreasure && distanceFromCatching < 0.3f)
                {
                    catchingTreasure = false;
                }
                if (catchingTreasure)
                {
                    fishPos = treasurePos;
                }
            }

            float strength = (fishPos - (barPos + barHeight / 2)) / 16f;
            if(fishPos > down || fishPos < up)
            {
                bobberBarSpeed = strength;
            }

            bobberSpeed.SetValue(bobberBarSpeed);
        }

        private float Cap(float f, float min, float max)
        {
            return f < min ? min : (f > max ? max : f);
        }

        private void Log(string format, params object[] args)
        {
            Monitor.Log(Format(format, args));
        }

        private string Format(string format, params object[] args)
        {
            return string.Format(format, args);
        }

        private List<FarmAnimal> GetAnimalsList(Player player)
        {
            List<FarmAnimal> list = new List<FarmAnimal>();
            if(player.currentLocation is Farm farm)
            {
                foreach(KeyValuePair<long,FarmAnimal> animal in farm.animals)
                {
                    list.Add(animal.Value);
                }
            }
            else if(player.currentLocation is AnimalHouse house)
            {
                foreach (KeyValuePair<long, FarmAnimal> animal in house.animals)
                {
                    list.Add(animal.Value);
                }
            }
            return list;
        }

        private void LetAnimalsInHome()
        {
            Farm farm = Game1.getFarm();
            foreach (KeyValuePair<long, FarmAnimal> kv in farm.animals.ToList())
            {
                FarmAnimal animal = kv.Value;
                animal.warpHome(farm, animal);
            }
        }

        private void ShowHUDMessage(string message, int duration = 3500)
        {
            HUDMessage hudMessage = new HUDMessage(message, 3)
            {
                noIcon = true,
                timeLeft = duration
            };
            Game1.addHUDMessage(hudMessage);
        }

        private RectangleE Expand(Rectangle rect, int radius)
        {
            return new RectangleE(rect.Left - radius, rect.Top - radius, 2 * radius, 2 * radius);
        }

        private void DrawSimpleTextbox(SpriteBatch batch, string text, SpriteFont font, Item item)
        {
            Vector2 stringSize = font.MeasureString(text);
            int x = Game1.getMouseX() - (int)(stringSize.X)/2;
            int y = Game1.getMouseY() + Game1.tileSize / 2;

            if(x < 0)
            {
                x = 0;
            }
            if(y < 0)
            {
                y = 0;
            }
            int rightX = (int)stringSize.X + Game1.tileSize / 2 + Game1.tileSize + 8;
            if(x + rightX > Game1.viewport.Width)
            {
                x = Game1.viewport.Width - rightX;
            }
            int bottomY = Math.Max(60, (int)(stringSize.Y + Game1.tileSize * 1.2));
            if(bottomY + y > Game1.viewport.Height)
            {
                y = Game1.viewport.Height - bottomY;
            }
            IClickableMenu.drawTextureBox(batch, Game1.menuTexture, new Rectangle(0, 256, 60, 60), x, y, rightX, bottomY, Color.White, 1f, true);
            if(!string.IsNullOrEmpty(text))
            {
                Vector2 vector2 = new Vector2(x + Game1.tileSize / 4, y + bottomY / 2 - 10);
                batch.DrawString(font, hoverText, vector2 + new Vector2(2f, 2f), Game1.textShadowColor, 0f, Vector2.Zero, 1.0f, SpriteEffects.None, 0f);
                batch.DrawString(font, hoverText, vector2 + new Vector2(0f, 2f), Game1.textShadowColor, 0f, Vector2.Zero, 1.0f, SpriteEffects.None, 0f);
                batch.DrawString(font, hoverText, vector2 + new Vector2(2f, 0f), Game1.textShadowColor, 0f, Vector2.Zero, 1.0f, SpriteEffects.None, 0f);
                batch.DrawString(font, hoverText, vector2, Game1.textColor * 0.9f, 0f, Vector2.Zero, 1.0f, SpriteEffects.None, 0f);
            }
            item.drawInMenu(batch, new Vector2(x + (int)stringSize.X + 24, y + 16), 1.0f,1.0f,0.9f,false);
        }
        
        #endregion
    }
}
