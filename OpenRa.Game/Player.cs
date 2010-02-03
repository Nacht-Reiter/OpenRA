using System;
using System.Linq;
using System.Collections.Generic;
using OpenRa.GameRules;
using OpenRa.Graphics;
using OpenRa.Traits;
using OpenRa.FileFormats;
using System.Drawing;

namespace OpenRa
{
	public enum PowerState { Normal, Low, Critical };

	public class Player
	{
		public Actor PlayerActor;
		public string Palette;
		public int Kills;
		public string PlayerName;
		public string InternalName;
		public Race Race;
		public readonly int Index;
		public int Cash = 10000;
		public int Ore = 0;
		public int OreCapacity;
		public int DisplayCash = 0;
		public int PowerProvided = 0;
		public int PowerDrained = 0;

		public World World { get { return PlayerActor.World; } }
		
		public Color Color;
		/*
		    Color.FromArgb(228, 200, 112),
			Color.FromArgb(56, 72, 125),
			Color.FromArgb(238, 0, 0),
			Color.FromArgb(198,97,0),
			Color.FromArgb(28,109,97),
			Color.FromArgb(153,76,53),
			Color.FromArgb(76,101,60),
			Color.FromArgb(133,113,101),
		 */
		public Shroud Shroud;

		public Player( World world, int index, Session.Client client )
		{
			Shroud = new Shroud(this, world.Map);
			this.PlayerActor = world.CreateActor("Player", new int2(int.MaxValue, int.MaxValue), this);
			this.Index = index;
			this.InternalName = "Multi{0}".F(index);

			this.Palette = client != null ? "player"+client.Palette : "player"+index;
			this.PlayerName = client != null ? client.Name : "Player {0}".F(index+1);
			this.Race = client != null ? (Race)client.Race : Race.Allies;
		}

		void UpdatePower()
		{
			var oldBalance = PowerProvided - PowerDrained;

			PowerProvided = 0;
			PowerDrained = 0;

			var myBuildings = World.Queries.OwnedBy[this]
				.WithTrait<Building>();

			foreach (var a in myBuildings)
			{
				var p = a.Trait.GetPowerUsage();
				if (p > 0)
					PowerProvided += p;
				else 
					PowerDrained -= p;
			}

			if (PowerProvided - PowerDrained < 0)
				if (PowerProvided - PowerDrained  != oldBalance)
					GiveAdvice("lopower1.aud");
		}
				
		public float GetSiloFullness()
		{
			return (float)Ore / OreCapacity;
		}

		public PowerState GetPowerState()
		{
			if (PowerProvided >= PowerDrained) return PowerState.Normal;
			if (PowerProvided > PowerDrained / 2) return PowerState.Low;
			return PowerState.Critical;
		}

		void UpdateOreCapacity()
		{
			OreCapacity = World.Queries.OwnedBy[this]
				.Where(a => a.traits.Contains<StoresOre>())
				.Select(a => a.Info.Traits.Get<StoresOreInfo>())
				.Sum(b => b.Capacity);
		}

		void GiveAdvice(string advice)
		{
			// todo: store the condition or something.
			// repeat after Rules.General.SpeakDelay, as long as the condition holds.
			Sound.PlayToPlayer(this, advice);
		}

		public void GiveCash( int num ) { Cash += num; }
		public void GiveOre(int num)
		{
			Ore += num;

			if (Ore > OreCapacity)
				Ore = OreCapacity;		// trim off the overflow.

			if (Ore > .8 * OreCapacity)
				GiveAdvice("silond1.aud");		// silos needed
		}

		public bool TakeCash( int num )
		{
			if (Cash + Ore < num) return false;
			if (Ore <= num)
			{
				num -= Ore;
				Ore = 0;
				Cash -= num;
			}
			else
				Ore -= num;
			
			return true;
		}

		const float displayCashFracPerFrame = .07f;
		const int displayCashDeltaPerFrame = 37;

		public void Tick()
		{
			UpdatePower();
			UpdateOreCapacity();
			Shroud.Tick( World );

			var totalMoney = Cash + Ore;
			var diff = Math.Abs(totalMoney - DisplayCash);
			var move = Math.Min(Math.Max((int)(diff * displayCashFracPerFrame),
					displayCashDeltaPerFrame), diff);

			if (DisplayCash < totalMoney)
			{
				DisplayCash += move;
				Sound.PlayToPlayer(this, "cashup1.aud");
			}
			else if (DisplayCash > totalMoney)
			{
				DisplayCash -= move;
				Sound.PlayToPlayer(this, "cashdn1.aud");
			}
		}

		public void SyncFromLobby(Session.Client client)
		{
			if (PlayerName != client.Name)
			{
				Game.chat.AddLine(this, "is now known as " + client.Name);
				PlayerName = client.Name;
			}

			if (Race != (Race)client.Race)
			{
				Game.chat.AddLine(this, "is now playing {0}".F((Race)client.Race));
				Race = (Race)client.Race;
			}

			if (Palette != client.Palette)
			{
				Game.chat.AddLine(this, "has changed color to {0}".F(client.Palette));
				Palette = client.Palette;
			}
		}
	}
}
