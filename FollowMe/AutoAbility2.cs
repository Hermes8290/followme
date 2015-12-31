using Lib_K_Relay;
using Lib_K_Relay.Interface;
using Lib_K_Relay.Networking;
using Lib_K_Relay.Networking.Packets;
using Lib_K_Relay.Networking.Packets.Client;
using Lib_K_Relay.Networking.Packets.DataObjects;
using Lib_K_Relay.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace AutoAbility
{
    public class AutoAbility : IPlugin
    {
        private List<Client> Clients = new List<Client>();
        private Classes[] _validClasses = { Classes.Rogue, Classes.Priest, Classes.Paladin, Classes.Warrior };

        public string GetAuthor()
        { return "KrazyShank / Kronks, edited by MeepDarknessMeep for FollowMe"; }

        public string GetName()
        { return "Auto Ability 2"; }

        public string GetDescription()
        {
            return "Automatically uses your abilities based on your class and your specified conditions:\n" +
                   "Paladin: Automatically Seal Buff\n" +
                   "Priest: Automatically Tome Buff and/or Heal\n" +
                   "Warrior: Automatically Helm Buff";
        }

        public string[] GetCommands()
        { return new string[] { }; }

        public void Initialize(Proxy proxy)
        {
            proxy.ClientConnected += (c) => Clients.Add(c);
            proxy.ClientDisconnected += (c) => Clients.Remove(c);

            proxy.HookPacket(PacketType.MOVE, Tick);
            proxy.HookPacket(PacketType.NEWTICK, Tick);
            proxy.HookPacket(PacketType.UPDATE, Tick);
        }

        private static XmlDocument xmldoc = null;

        private int GetMpCost(int objecttype)
        {
            if (objecttype == -1)
                return 0xFFFFF;


            if (xmldoc == null)
            {
                string path = Directory.GetCurrentDirectory() + @"/XML/items.xml";
                if (File.Exists(path))
                {
                    xmldoc = new XmlDocument();
                    xmldoc.Load(path);
                }
            }

            foreach (XmlNode childNode in xmldoc.DocumentElement.ChildNodes)
                if (childNode.Name == "Object" && 
                    Convert.ToUInt16(childNode.Attributes.GetNamedItem("type").Value, 16) == objecttype)

                    foreach (XmlNode attr in childNode.ChildNodes)
                        if (attr.Name == "MpCost")
                            return Convert.ToInt32(attr.InnerText);

            return 0xFFFF;
        }


        private void Tick(Client client, Packet packet)
        {
            if (client.PlayerData == null)
                return;

            int ManaCount = client.PlayerData.Mana;
            int ManaNeeded = GetMpCost(client.PlayerData.Slot[1]) + 2;

            bool NeedHealth = false;

            foreach (Client c in Clients)
                NeedHealth = NeedHealth || ((float)client.PlayerData.Health / (float)client.PlayerData.MaxHealth) < .55;

            if (ManaNeeded > ManaCount)
                return;

            switch (client.PlayerData.Class)
            {
                case Classes.Paladin:
                {
                    if (!client.PlayerData.HasConditionEffect(ConditionEffects.Damaging))
                        SendUseItem(client);

                    break;
                }
                case Classes.Priest:
                {
                    if (!client.PlayerData.HasConditionEffect(ConditionEffects.Healing) && NeedHealth)
                        SendUseItem(client);

                    break;
                }
                case Classes.Warrior:
                {
                    if (!client.PlayerData.HasConditionEffect(ConditionEffects.Berserk))
                        SendUseItem(client);

                    break;
                }
                case Classes.Rogue:
                {
                    if (!client.PlayerData.HasConditionEffect(ConditionEffects.Invisible))
                        SendUseItem(client);

                    break;
                }
            }
        }

        private void SendUseItem(Client client)
        {
            UseItemPacket useItem = (UseItemPacket)UseItemPacket.Create(PacketType.USEITEM);
            useItem.Time = client.Time;
            useItem.ItemUsePos = new Location();
            useItem.ItemUsePos.X = client.PlayerData.Pos.X;
            useItem.ItemUsePos.Y = client.PlayerData.Pos.Y;
            useItem.SlotObject = new SlotObject();
            useItem.SlotObject.SlotId = 1;
            useItem.SlotObject.ObjectType = (short)client.PlayerData.Slot[1];
            useItem.SlotObject.ObjectId = client.ObjectId;
            useItem.UseType = 1;
            client.SendToServer(useItem);
            
            client.SendToClient(
                PluginUtils.CreateNotification(client.ObjectId, "Auto-Buff Triggered!")
            );
        }
    }
}