using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Reflection;

using Rust;

using Network.Visibility;
using Oxide.Core;
using Rust;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    #region zone
    public class Zone
    {
        public List<int> Bots;

        public virtual void Start()
        {

        }
    }
    #endregion

    #region bot
    public class Bot
    {
        public ulong Id;
        public int ZoneId;

        public float PosX;
        public float PosY;
        public float PosZ;

        private BasePlayer BasePlayer;

        public Bot(int zone, ulong id, Vector3 position)
        {
            Id = id;

            PosX = position.x;
            PosY = position.y;
            PosZ = position.z;
        }

        #region main methods
        public void Spawn()
        {
            AimPractice.Instance.Chat("Spawning!");

            var newPlayer = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", GetPos(), Quaternion.identity);
            newPlayer.Spawn();

            newPlayer.SetFlag(BaseEntity.Flags.Reserved1, true);
            FieldInfo modelStateField = typeof(BasePlayer).GetField("modelState", BindingFlags.Instance | BindingFlags.NonPublic);
            object modelState = modelStateField.GetValue(newPlayer);
            modelState.GetType().GetProperty("onground").SetValue(modelState, true, null);

            newPlayer.SendNetworkUpdate();

            BasePlayer = newPlayer.GetComponent<BasePlayer>();
            BasePlayer.userID = Id;

            AimPractice.Instance.Chat("Spawned!");
        }

        public void Kill()
        {
            if(BasePlayer != null)
            {
                BasePlayer.Kill();
                BasePlayer.SendNetworkUpdate();
            }
        }

        #endregion

        #region utility methods
        Vector3 GetPos()
        {
            return new Vector3(PosX, PosY, PosZ);
        }
        #endregion
    }
    #endregion

    #region plugin
    [Info("AimPractice", "Rob", 0.1)]
    [Description("Practice your aim against moving targets!")]
    public class AimPractice : RustPlugin
    {
        #region data
        public static AimPractice Instance;
        public static HashSet<Bot> Bots;
        public static bool Moving = true;
        #endregion

        #region public static main methods
        public static void CreateBot(Vector3 position)
        {
            Bot bot = new Bot(0, (ulong) Bots.Count, position);

            bot.Spawn();

            Bots.Add(bot);

            AimPractice.SaveBots();
        }

        public static void LoadBots()
        {
            Bots = Interface.Oxide.DataFileSystem.ReadObject<HashSet<Bot>>("Bots");
        }

        public static void SaveBots()
        {
            Interface.Oxide.DataFileSystem.WriteObject<HashSet<Bot>>("Bots", Bots, true);
        }

        private void ResetTime(BaseEntity entity)
        {
            var corpse = entity as BaseCorpse;
            if (!corpse) return;
            if (!(corpse is PlayerCorpse) && !corpse?.parentEnt?.ToPlayer()) return;

            timer.Once(1, () =>
                {
                    if (!corpse.IsDestroyed)
                        corpse.ResetRemovalTime(0);
                });
        }
        #endregion

        #region main methods
        public void Chat(string chat)
        {
            PrintToChat(chat);
        }

        void UnlimitedAmmo(BaseProjectile projectile, BasePlayer player)
        {
            projectile.GetItem().condition = projectile.GetItem().info.condition.max;
            projectile.primaryMagazine.contents = projectile.primaryMagazine.capacity;
            projectile.SendNetworkUpdateImmediate();
        }
        #endregion

        #region oxide hooks
        void OnFrame()
        {
            BasePlayer[] players = GameObject.FindObjectsOfType<BasePlayer>();

            foreach (BasePlayer player in players)
            {
                if (player.IsWounded() && player.HasFlag(BaseEntity.Flags.Reserved1))
                {
                    player.Kill();
                    player.SendNetworkUpdate();
                }
                 
            }

        }
        int killCount = 0;
        void OnPlayerDie ()
        {
            var sizeOfList = locations.Count;
            killCount++;
            //AutoTurret[] turrets = GameObject.FindObjectsOfType<AutoTurret>();
            //AutoTurret randomTurret = turrets[UnityEngine.Random.Range(0, turrets.Length - 1)];
            SpawnBot(locations[UnityEngine.Random.Range(0, sizeOfList - 1)]);
            return;
        }
        

        private void OnEntitySpawned(BaseEntity entity) => ResetTime(entity);

        private void OnEntityTakeDamage(BaseEntity entity, HitInfo info) => ResetTime(entity);

        private void OnLootEntityEnd(BasePlayer looter, BaseEntity entity) => ResetTime(entity);
        /*
        Vector3 position;

        void OnPlayerInit(BasePlayer player)
        {
            Puts("OnPlayerInit works!");
            player.MovePosition(position);
        }    

        void OnPlayerRespawned(BasePlayer player)
        {
            Puts("OnPlayerRespawned works!");
        }
        */
        void Loaded()
        {
            AimPractice.Instance = this;
            AimPractice.LoadBots();
        }

        void OnWeaponFired(BaseProjectile projectile, BasePlayer player)
        {
                UnlimitedAmmo(projectile, player);
                misses++;
        }

        int headShots;
        int hits;
        int misses;
        
        void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            BasePlayer victim = info.HitEntity.GetComponent<BasePlayer>();

            if (victim == null || info == null) return;
            DamageType type = info.damageTypes.GetMajorityDamageType();
            if (type == null) return;
            if (info.HitEntity != null && info.Initiator != null && info.Initiator.ToPlayer() != null) {
                
                    if(victim.HasFlag(BaseEntity.Flags.Reserved1))
                    {
                        hits++;
                        misses--;
                        if (info.isHeadshot)
                        {
                            headShots++;
                        }
                        return;
                }
            }
        }
        /*
        void OnEntityTakeDamage(BaseCombatEntity victim, HitInfo info)
        {
            if (victim == null || info == null) return;
            DamageType type = info.damageTypes.GetMajorityDamageType();
            if (type == null) return;

            if (info.Initiator != null && info.Initiator.ToPlayer() != null)
            {
             if(victim.HasFlag(BaseEntity.Flags.Reserved1))
                    {
                        hits++;
                        misses--;
                        if (info.isHeadshot)
                        {
                            headShots++;
                        
                        }
                        return;
                }   
            }
        }
        */

        #endregion
        #region commands
        [ConsoleCommand("createBot")]
        void CmdCreateBot(ConsoleSystem.Arg arg)
        {
            AimPractice.CreateBot(arg.Player().transform.position);
            PrintToChat("Where them bots at?");
        }
        
        [ConsoleCommand("killBots")]
        void CmdKillBots(ConsoleSystem.Arg arg)
        {
            foreach(Bot bot in Bots)
            {
                bot.Kill();
            }
        }

        public static List<Vector3> locations = new List<Vector3>();

        [ConsoleCommand("s")]
        void CmdS(ConsoleSystem.Arg arg)
        {
            //AutoTurret[] turrets = GameObject.FindObjectsOfType<AutoTurret>();
            //Vector3 randomLocation = locations[UnityEngine.Random.Range(0, locations.Length - 1)];
            var sizeOfList = locations.Count;
            if (sizeOfList > 0) {
                PrintToChat("Bot spawned on location » " + locations[UnityEngine.Random.Range(0, sizeOfList - 1)]);
                SpawnBot(locations[UnityEngine.Random.Range(0, sizeOfList - 1)]);
            }else {
                PrintToChat("<color=#FF5555>Please set bot locations first.</color>");
            }
            
        }

        AutoTurret[] turrets;

        [ConsoleCommand("cl")]
        void CmdCl(ConsoleSystem.Arg arg)
        {
            AutoTurret[] turrets = GameObject.FindObjectsOfType<AutoTurret>();
            
            int length = turrets.Length;
            PrintToChat("Spawns » " + length);

            for (int i = 0; i < length; i++)
            {
                locations.Add(turrets[i].transform.position);
            }

            foreach(var item in locations )
            {
                PrintToChat("Location » " + item);
            }
            foreach (AutoTurret autoturret in turrets)
            {
                autoturret.Kill();
                autoturret.SendNetworkUpdate();
            }
        }

        [ConsoleCommand("cla")]
        void CmdCla(ConsoleSystem.Arg arg)
        {
            locations.Clear();
        }


        [ConsoleCommand("spawn")]
        void CmdSpawn(ConsoleSystem.Arg arg)
        {
            StashContainer[] stashes = GameObject.FindObjectsOfType<StashContainer>();
            StashContainer randomStash = stashes[UnityEngine.Random.Range(0, stashes.Length - 1)];
            SpawnBot(randomStash.transform.position);
        }


        [ChatCommand("stats")]
        void CmdChatStats(BasePlayer player, string command, string[] args)
        {
            double accuracy = (double)hits/misses;
            double hsRatio = (double)headShots/hits;
            PrintToChat("<color=#FF5555>Stats:</color>");
            PrintToChat("<color=#AAAAAA>------------------------</color>");
            PrintToChat("<color=#5555FF>Kills</color> <color=#555555>»</color> <color=#FF5555>" + killCount + "</color>");
            PrintToChat("<color=#AAAAAA>------------------------</color>");
            PrintToChat("<color=#5555FF>Hits</color> <color=#555555>»</color> <color=#FFFF55>" + hits + "</color>");
            PrintToChat("<color=#5555FF>Misses</color> <color=#555555>»</color> <color=#FFFF55>" + misses + "</color>");
            PrintToChat("<color=#5555FF>Headshots</color> <color=#555555>»</color> <color=#FFFF55>" + headShots + "</color>");
            PrintToChat("<color=#AAAAAA>------------------------</color>");
            PrintToChat("<color=#5555FF>Accuracy</color> <color=#555555>»</color> <color=#FF5555>" + String.Format("{0:P2}", accuracy) + "</color>");
            PrintToChat("<color=#5555FF>HS Ratio</color> <color=#555555>»</color> <color=#FF5555>" + String.Format("{0:P2}", hsRatio) + "</color>");
        }

        Vector3 location;

        [ChatCommand("setspawn")]
        void CmdChatSetSpawn(BasePlayer player, string command, string[] args)
        {
            //Vector3 location = player.transform.position;
            Vector3 location = bot.GetPos();
            PrintToChat("Spawn set at " + location);
        }

        [ChatCommand("spawn")]
        void CmdChatSpawn(BasePlayer player, string command, string[] args)
        {
            if (location == null) return;
            PrintToChat("Teleporting " + location);
            player.MovePosition(location);
        }

        [ConsoleCommand("stash")]
        void CmdStash(ConsoleSystem.Arg arg)
        {
            StashContainer[] stashes = GameObject.FindObjectsOfType<StashContainer>();
            foreach (StashContainer stash in stashes)
            {
                stash.Kill();
                stash.SendNetworkUpdate();
            }

            BasePlayer[] players = GameObject.FindObjectsOfType<BasePlayer>();
            foreach(BasePlayer player in players)
            {
                if (player.HasFlag(BaseEntity.Flags.Reserved1))
                {
                    player.Kill();
                    player.SendNetworkUpdate();
                }
            }
        }

        [ConsoleCommand("move")]
        void CmdMove(ConsoleSystem.Arg arg)
        {
            AimPractice.Moving = !AimPractice.Moving;
        }

        DateTime startTime;

        [ConsoleCommand("clean")]
        void CmdClean(ConsoleSystem.Arg arg)
        {
            startTime = DateTime.Now;

            BasePlayer[] players = GameObject.FindObjectsOfType<BasePlayer>();
            foreach(BasePlayer player in players)
            {
                if (player.HasFlag(BaseEntity.Flags.Reserved1))
                {
                    player.Kill();
                    player.SendNetworkUpdate();
                }
            }

            BaseCorpse[] corpses = GameObject.FindObjectsOfType<BaseCorpse>();
            foreach(BaseCorpse corpse in corpses)
            {
                corpse.Kill();
                corpse.SendNetworkUpdate();
            }
        }

        void SpawnBot(Vector3 position)
        {

            var newPlayer = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", position, Quaternion.identity);
            newPlayer.Spawn();
            newPlayer.gameObject.AddComponent<BotMover>();

            newPlayer.SetFlag(BaseEntity.Flags.Reserved1, true);
            FieldInfo modelStateField = typeof(BasePlayer).GetField("modelState", BindingFlags.Instance | BindingFlags.NonPublic);
            object modelState = modelStateField.GetValue(newPlayer);
            modelState.GetType().GetProperty("onground").SetValue(modelState, true, null);

            newPlayer.SendNetworkUpdate();

        }
        #endregion
    }
    #endregion

    #region
    public class BotMover : MonoBehaviour
    {
        BasePlayer basePlayer;
        Vector3 startPosition;
        Vector3 targetPosition;
        float moveSpeed = 1.0f;
        void Start()
        {
            basePlayer = GetComponent<BasePlayer>();
            startPosition = transform.position;
            targetPosition = startPosition;
            moveSpeed = UnityEngine.Random.Range(0.1f, 0.2f);
            basePlayer.ChangeHealth(100.0f);
            basePlayer.SendNetworkUpdate();

            int random = UnityEngine.Random.Range(0, 3);
            int clothes = UnityEngine.Random.Range(0, 2);

            if (random == 0)//Metal
            {
                basePlayer.inventory.GiveItem(ItemManager.CreateByItemID(-46848560), basePlayer.inventory.containerWear);
                basePlayer.inventory.GiveItem(ItemManager.CreateByItemID(1265861812), basePlayer.inventory.containerWear);
                basePlayer.inventory.GiveItem(ItemManager.CreateByItemID(-1595790889), basePlayer.inventory.containerWear);
            }
            else if (random == 1)
            {
                int random2 = UnityEngine.Random.Range(0, 2);
                if (random2 == 0)
                {
                    basePlayer.inventory.GiveItem(ItemManager.CreateByItemID(1260209393), basePlayer.inventory.containerWear);
                }
                else if (random2 == 1)
                {
                    basePlayer.inventory.GiveItem(ItemManager.CreateByItemID(-2128719593), basePlayer.inventory.containerWear);
                }

                basePlayer.inventory.GiveItem(ItemManager.CreateByItemID(-288010497), basePlayer.inventory.containerWear);
                basePlayer.inventory.GiveItem(ItemManager.CreateByItemID(-1595790889), basePlayer.inventory.containerWear);
            }

            if (clothes == 0)
            {
                basePlayer.inventory.GiveItem(ItemManager.CreateByItemID(115739308), basePlayer.inventory.containerWear);
                basePlayer.inventory.GiveItem(ItemManager.CreateByItemID(707427396), basePlayer.inventory.containerWear);
                basePlayer.inventory.GiveItem(ItemManager.CreateByItemID(1767561705), basePlayer.inventory.containerWear);
            }
            if (clothes == 1)
            {
                basePlayer.inventory.GiveItem(ItemManager.CreateByItemID(106433500), basePlayer.inventory.containerWear);
                basePlayer.inventory.GiveItem(ItemManager.CreateByItemID(-1211618504), basePlayer.inventory.containerWear);
                basePlayer.inventory.GiveItem(ItemManager.CreateByItemID(115739308), basePlayer.inventory.containerWear);
            }
        }

        
        
        void FixedUpdate()
        {
            if (!AimPractice.Moving)
            {
                return;
            }

            if (Vector3.Distance(transform.position, targetPosition) < 1.0f)
            {
                //targetPosition = startPosition + transform.right * UnityEngine.Random.Range(-15, 15);
                //AutoTurret[] turrets = GameObject.FindObjectsOfType<AutoTurret>();
                //AutoTurret randomTurret = turrets[UnityEngine.Random.Range(0, turrets.Length - 1)];
                //targetPosition = randomTurret.transform.position;
                var sizeOfList = AimPractice.locations.Count;
                targetPosition = AimPractice.locations[UnityEngine.Random.Range(0, sizeOfList - 1)];
            }

            Vector3 newPos = Vector3.Lerp(transform.position, targetPosition, Time.fixedDeltaTime * moveSpeed);
            newPos = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed);
            basePlayer.transform.position = newPos;
            basePlayer.Teleport(basePlayer);
            basePlayer.SendNetworkUpdate();
        }
        
    }
    #endregion
}