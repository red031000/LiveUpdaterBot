using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace StreamFeedBot
{
	public class RunStatus
	{
		[JsonProperty(PropertyName = "area_name")]
		public string AreaName;

		//[JsonProperty(PropertyName = "badges")]
		[JsonIgnore] //Gen 7 doesn't have badges
		public uint Badges;

		[JsonIgnore]
		public List<bool> BadgesFlags = new List<bool>();

		[JsonProperty(PropertyName = "game_stats")]
		public GameStats GameStats;

		[JsonProperty(PropertyName = "seen")]
		public uint Seen;

		[JsonProperty(PropertyName = "map_name")]
		public string MapName;

		[JsonProperty(PropertyName = "map_id")]
		public uint MapId;

		[JsonProperty(PropertyName = "battle_kind", ItemConverterType = typeof(StringEnumConverter))]
		public BattleKind? BattleKind;

		[JsonProperty(PropertyName = "enemy_party")]
		public List<Pokemon> EnemyParty;

		[JsonProperty(PropertyName = "enemy_trainers")]
		public List<Trainer> EnemyTrainers;

		[JsonProperty(PropertyName = "party")]
		public List<Pokemon> Party;

		[JsonProperty(PropertyName = "battle_party")]
		public List<Pokemon> BattleParty;

		[JsonProperty(PropertyName = "items")]
		public ItemGroup Items;

		[JsonProperty(PropertyName = "gender")]
		public Gender? Gender;

		[JsonProperty(PropertyName = "name")]
		public string Name;

		[JsonProperty(PropertyName = "pc")]
		public PC PC;

		[JsonProperty(PropertyName = "daycare")]
		public List<Pokemon> Daycare;
	}

	public class PC
	{
		[JsonProperty(PropertyName = "boxes")]
		public List<Box> Boxes;

		[JsonProperty(PropertyName = "current_box_number")]
		public uint CurrentBoxNumber;
	}

	public class Box
	{
		[JsonProperty(PropertyName = "box_contents")]
		public List<Pokemon> BoxContents;

		[JsonProperty(PropertyName = "box_name")]
		public string BoxName;

		[JsonProperty(PropertyName = "box_number")]
		public uint BoxNumber;
	}

	public class ItemGroup
	{
		//[JsonProperty(PropertyName = "balls")]
		[JsonIgnore] //not in Gen 7
		public List<Item> Balls;

		[JsonProperty(PropertyName = "berries")]
		public List<Item> Berries;

		[JsonProperty(PropertyName = "medicine")] //Gen 7 specific
		public List<Item> Medicine;

		[JsonProperty(PropertyName = "items")]
		public List<Item> Items;

		[JsonProperty(PropertyName = "key")]
		public List<Item> Key;

		[JsonProperty(PropertyName = "tms")]
		public List<Item> TMs;
		
		[JsonProperty(PropertyName = "z_crystals")] //Gen 7 specific
		public List<Item> ZCrystals;
	}

	public class Item
	{
		[JsonProperty(PropertyName = "count")]
		public uint? Count;

		[JsonProperty(PropertyName = "id")]
		public uint Id;

		[JsonProperty(PropertyName = "name")]
		public string Name;
	}

	public class Pokemon
	{
		[JsonProperty(PropertyName = "active")]
		public bool? Active;

		[JsonProperty(PropertyName = "level")]
		public uint? Level;

		[JsonProperty(PropertyName = "name")]
		public string Name;

		[JsonProperty(PropertyName = "personality_value")]
		public uint PersonalityValue;

		[JsonProperty(PropertyName = "gender")]
		public Gender? Gender;

		[JsonProperty(PropertyName = "species")]
		public Species Species;

		[JsonProperty(PropertyName = "held_item")]
		public Item HeldItem;

		[JsonProperty(PropertyName = "moves")]
		public List<Move> Moves;

		[JsonProperty(PropertyName = "health")]
		public int[] Health;
	}

	public class Species
	{
		[JsonProperty(PropertyName = "name")]
		public string Name;

		[JsonProperty(PropertyName = "id")]
		public int Id;
	}

	public class Move
	{
		[JsonProperty(PropertyName = "id")]
		public int Id;

		[JsonProperty(PropertyName = "name")]
		public string Name;
	}

	public class Trainer
	{
		[JsonProperty(PropertyName = "class_name")]
		public string ClassName;

		[JsonProperty(PropertyName = "gender")]
		public Gender? Gender;

		[JsonProperty(PropertyName = "name")]
		public string Name;

		[JsonProperty(PropertyName = "id")]
		public int Id;

		[JsonProperty(PropertyName = "class_id")]
		public int ClassId;
	}

	public class GameStats
	{
		[JsonProperty(PropertyName = "blackouts")]
		public uint Blackouts;

		//[JsonProperty(PropertyName = "Saves Made")]
		[JsonIgnore] //Not in USUM
		public uint Saves;

		//[JsonProperty(PropertyName = "Pok\u00e9mon Center Uses")]
		[JsonIgnore] //Not in USUM
		public uint PokemonCentersUsed;

		[JsonProperty(PropertyName = "Battles Fought (Total)")]
		public uint BattlesFought;
	}

	public enum BattleKind
	{
		Wild,
		Trainer
	}

	public enum Gender
	{
		Male,
		Female
	}
}
