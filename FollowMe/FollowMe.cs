using Lib_K_Relay;
using Lib_K_Relay.Interface;
using Lib_K_Relay.Networking;
using Lib_K_Relay.Networking.Packets;
using Lib_K_Relay.Utilities;
using Lib_K_Relay.Networking.Packets.Server;
using Lib_K_Relay.Networking.Packets.Client;
using Lib_K_Relay.Networking.Packets.DataObjects;

using System;
using System.Collections.Generic;
using System.Windows.Forms;


namespace FollowMe
{


    public class FollowMe : IPlugin
    {

        private Dictionary<Client, int> TeleportState = new Dictionary<Client, int>();
        private Dictionary<Client, int> Time = new Dictionary<Client, int>();
        private Dictionary<Client, List<int>> GotoSkipList = new Dictionary<Client, List<int>>();
        private FMList<Client> Clients = new FMList<Client>();
        private Dictionary<Client, int> LastManualGoto = new Dictionary<Client, int>();

        private Dictionary<Client, FMList<Entity>> Dungeons = new Dictionary<Client, FMList<Entity>>();

        private List<Client> TeleportQueue;

        private List<Client> GotoQueue = new List<Client>();

        private string MasterID = null;

        private int MinimumSpeed = int.MaxValue;
        
        private Client Master = null; 

        public string GetAuthor()
        {
            return "MeepDarknessMeep";
        }

        public string[] GetCommands()
        {
            return new string[] { "/followme" };
        }

        public string GetDescription()
        {
            return "Make all clients follow another client (will limit speed to slowest person!)";
        }

        public string GetName()
        {
            // Please return in date format
            // YYYYMMDD
            return "Follower v20151230";
        }

        public void Initialize(Proxy proxy)
        {

            HookManager.KeyUp += this.OnKeyUp;
            HookManager.KeyDown += this.OnKeyDown;

            proxy.HookCommand("followme", this.OnFollowMe);


            proxy.HookPacket(PacketType.QUESTOBJID, (c, p) => { TeleportState[c] = 0; });
            proxy.HookPacket(PacketType.MOVE, this.proxy_Move);

            proxy.HookPacket(PacketType.GOTOACK, this.proxy_GotoAck);
            proxy.HookPacket(PacketType.NEWTICK, this.proxy_NewTick);

            proxy.HookPacket(PacketType.GOTO, this.proxy_Goto);
            proxy.HookPacket(PacketType.USEPORTAL, this.proxy_UsePortal);
            proxy.HookPacket(PacketType.UPDATE, this.proxy_Update);

            proxy.ClientConnected += this.proxy_ClientConnected;
            proxy.ClientDisconnected += this.proxy_ClientDisconnected;

        }

        private void proxy_Goto(Client client, Packet real_packet)
        {

            GotoSkipList[client].Add(0);

        }

        private void proxy_NewTick(Client client, Packet real_packet)
        {

            var packet = (NewTickPacket)real_packet;

            if (client.PlayerData != null && client.PlayerData.Speed < MinimumSpeed)
                MinimumSpeed = client.PlayerData.Speed;

            if (MasterID != null && client.PlayerData != null && client.PlayerData.AccountId == MasterID)
                Master = client;
            
            if (Master != null)
            {
                Status statdata = null;

                for (int i = 0; i < packet.Statuses.Length; i++)
                {

                    if (packet.Statuses[i].ObjectId == client.ObjectId)
                        statdata = packet.Statuses[i];

                }

                if (statdata == null)
                {

                    var old = packet.Statuses;

                    packet.Statuses = new Status[old.Length + 1];

                    for (var i = 0; i < old.Length; i++)
                        packet.Statuses[i] = old[i];

                    statdata = FMUtil.GenerateStatus(client.PlayerData.Pos, 1, client.ObjectId);
                    FMUtil.SetStatData(statdata.Data[0], StatsType.Speed, MinimumSpeed,  "Speed");

                    packet.Statuses[old.Length] = statdata;

                    return;

                }


                bool ret = false;
                for (int i = 0; i < statdata.Data.Length; i++)
                {
                    if (statdata.Data[i].Id == StatsType.Effects)
                        statdata.Data[i].IntValue &= ~(
                            (int)ConditionEffects.Speedy | (int)ConditionEffects.AnotherSpeedy
                        );
                    
                    else if (statdata.Data[i].Id == StatsType.Speed)
                    {
                        statdata.Data[i].IntValue = MinimumSpeed;
                        ret = true;
                    }
                }

                if (ret)
                    return;

                StatData[] old2 = statdata.Data;

                statdata.Data = new StatData[old2.Length + 1];

                for (int i = 0; i < old2.Length; i++)
                    statdata.Data[i] = old2[i];

                statdata.Data[old2.Length] = FMUtil.CreateStatData(StatsType.Speed, MinimumSpeed, "Speed");

            }

        }


        private void proxy_UsePortal(Client client, Packet real_packet)
        {

            UsePortalPacket packet = (UsePortalPacket)real_packet;
            
            TeleportQueue = new List<Client>();

            foreach (var c in Clients)
            {
                
                if (c == Master)
                    continue;

                TeleportQueue.Add(c);

            };

            TeleportQueue.Add(Master);

            var cl = TeleportQueue[0];
            TeleportQueue.Remove(cl);

            packet.ObjectId = Dungeons[cl].Choose(
                (x1, x2) => x1 < x2,
                (obj) => obj.Status.Position.DistanceSquaredTo(cl.PlayerData.Pos),
                4
            ).Status.ObjectId;

            cl.SendToServer(packet);

            packet.Send = false;

        }

        private void proxy_Update(Client client, Packet real_packet)
        {

            UpdatePacket packet = (UpdatePacket)real_packet;

            for (int i = 0; i < packet.Tiles.Length; i++)
                if (packet.Tiles[i].Type == 0xb8)
                    packet.Tiles[i].Type = 0x96;


            for (int i = 0; i < packet.NewObjs.Length; i++)
            {

                Entity obj = packet.NewObjs[i];
                

                for (int c = 0; c < obj.Status.Data.Length; c++)
                {

                    StatData data = obj.Status.Data[c];

                    if (data.Id == StatsType.PortalUsable && data.IntValue != 0)
                         Dungeons[client].Add(obj);

                    else if (data.Id == StatsType.PortalUsable && data.IntValue == 0)
                    {

                        foreach (Entity e in Dungeons[client])
                            if (e.Status.ObjectId == obj.Status.ObjectId)
                                Dungeons[client].Remove(e);

                    }

                }

            }

        }

        private static Location RotateVector(Location pos, double angle)
        {
            Location ret = new Location();

            double ca = Math.Cos(angle), sa = Math.Sin(angle);

            ret.X = (float)(ca * (pos.X) - sa * pos.Y);
            ret.Y = (float)(ca * pos.Y + sa * pos.X);

            return ret;

        }

        private void proxy_Move(Client client, Packet real_packet)
        {
            
            MovePacket packet = (MovePacket)real_packet;
            
            if (Master == null)
            {
                
                Time[client] = packet.Time;

                return;
            }

            if (Master != client)
            {
                if (client.PlayerData.Pos.DistanceTo(Master.PlayerData.Pos) > 2)
                {

                    if (TeleportState[client] > 1)
                        TeleportState[client]--;
                    else if (TeleportState[client] == 0)
                    {

                        TeleportPacket tp = (TeleportPacket)TeleportPacket.Create(PacketType.TELEPORT);

                        tp.ObjectId = Master.ObjectId;

                        client.SendToServer(tp);

                        TeleportState[client] = 10000 / 200;

                    }

                    /*Master.SendToClient(
                        PluginUtils.CreateNotification(Master.ObjectId,
                        "Player " + client.PlayerData.Name + 
                        " is too far away from you to reposition!")
                        );*/

                    return;
                }

                // TODO: fix?
                /*
                if (packet.Records.Length == 0)
                    return;

                double angle = 180 / Math.PI * Math.Atan2(packet.NewPosition.Y - packet.Records[0].Y,
                    packet.NewPosition.X - packet.Records[0].X);
                if (angle % 90 == 0)
                {
                */
                    
                if (packet.Records.Length > 0 && 
                    packet.Records[0].X == packet.NewPosition.X &&
                    packet.Records[0].Y == packet.NewPosition.Y)

                    GotoQueue.Add(client);


            }
            else
            {
                
                while(GotoQueue.Count > 0)
                {

                    Client c = GotoQueue[0];
                    GotoQueue.RemoveAt(0);

                    GotoPacket gotop = (GotoPacket)GotoPacket.Create(PacketType.GOTO);
                    
                    gotop.Location = new Location();
                    
                    gotop.Location.X = Master.PlayerData.Pos.X;
                    gotop.Location.Y = Master.PlayerData.Pos.Y;
                    /*
                    {
                        double rads = Math.PI / 180 * angle;

                        Location MasterRotated = RotateVector(Master.PlayerData.Pos, -rads);
                        Location MeRotated = RotateVector(packet.NewPosition, -rads);
                        MasterRotated.X -= MeRotated.X;
                        MasterRotated.Y = 0;

                        Location Rotated = RotateVector(MasterRotated, rads);
                        
                        if (Math.Abs(Rotated.X + Rotated.Y) < 0.04)
                            return;

                        gotop.Location.X = packet.NewPosition.X - Rotated.X;
                        gotop.Location.Y = packet.NewPosition.Y - Rotated.Y;
                    }
                    */
                    gotop.ObjectId = c.ObjectId;

                    c.SendToClient(gotop);


                    GotoSkipList[c].Add(1);

                }
                
            }
        }


        private void proxy_GotoAck(Client client, Packet real_packet)
        {

            GotoAckPacket pack = (GotoAckPacket)real_packet;

            if (GotoSkipList[client][0] != 0)
                pack.Send = false;

            GotoSkipList[client].RemoveAt(0);

        }


        private void OnFollowMe(Client client, string command, string[] args)
        {

            Master = client;
            MasterID = client.PlayerData.AccountId;

        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (Master == null || !FMUtil.IsFollowMeTargetActive())
                return;
            
             FMUtil.SendKeyToAll(e.KeyCode, true);
        }
        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (Master == null || !FMUtil.IsFollowMeTargetActive())
                return;

            FMUtil.SendKeyToAll(e.KeyCode, false);
        }

        private void proxy_ClientConnected(Client client)
        {

            GotoSkipList.Add(client, new List<int>());
            Dungeons.Add(client, new FMList<Entity>());
            TeleportState.Add(client, -1);
            Time.Add(client, 0);
            LastManualGoto.Add(client, 0);

            Clients.Add(client);

            if (TeleportQueue != null)
            {
                // we are in the process of teleporting everyone to a new realm
               
                var cl = TeleportQueue[0];
                
                // Master is teleported last
                if (TeleportQueue.Count == 1)
                {
                    MasterID = cl.PlayerData.AccountId;
                    TeleportQueue = null;
                }
                else
                    TeleportQueue.Remove(cl);

                var packet = (UsePortalPacket)UsePortalPacket.Create(PacketType.USEPORTAL);
                packet.ObjectId = Dungeons[cl].Choose(
                    (x1, x2) => x1 < x2,
                    (Entity obj) => obj.Status.Position.DistanceSquaredTo(cl.PlayerData.Pos),
                    4
                ).Status.ObjectId;

                cl.SendToServer(packet);
            
            }

        }

        private void proxy_ClientDisconnected(Client client)
        {

            GotoSkipList.Remove(client);
            Dungeons.Remove(client);
            Time.Remove(client);
            TeleportState.Remove(client);
            LastManualGoto.Remove(client);

            if (Master == client)
                Master = null;

            Clients.Remove(client);

        }
        
        
    }
}
