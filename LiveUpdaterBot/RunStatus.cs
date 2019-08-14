using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LiveUpdaterBot
{
	public class RunStatus
	{
		[JsonProperty(PropertyName = "area_name")]
		public string AreaName;

		[JsonProperty(PropertyName = "badges")]
		public int Badges;

		[JsonIgnore]
		public List<bool> BadgesFlags = new List<bool>();

		[JsonProperty(PropertyName = "game_stats")]
		public GameStats GameStats;

		[JsonProperty(PropertyName = "seen")]
		public int Seen;

		[JsonProperty(PropertyName = "map_name")]
		public string MapName;

		[JsonProperty(PropertyName = "map_id")]
		public int MapId;

		[JsonProperty(PropertyName = "battle_kind", ItemConverterType = typeof(StringEnumConverter))]
		public BattleKind? BattleKind;

		[JsonProperty(PropertyName = "enemy_party")]
		public List<Pokemon> EnemyParty;

		[JsonProperty(PropertyName = "enemy_trainers")]
		public List<Trainer> EnemyTrainers;

		[JsonProperty(PropertyName = "party")]
		public List<Pokemon> Party;
	}

	public class Pokemon
	{
		[JsonProperty(PropertyName = "active")]
		public bool? Active;

		[JsonProperty(PropertyName = "level")]
		public int? Level;

		[JsonProperty(PropertyName = "name")]
		public string Name;

		[JsonProperty(PropertyName = "personality_value")]
		public uint PersonalityValue;

		[JsonProperty(PropertyName = "gender")]
		public Gender? Gender;

		[JsonProperty(PropertyName = "species")]
		public Species Species;

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
		public int Blackouts;

		[JsonProperty(PropertyName = "Saves Made")]
		public int Saves;

		[JsonProperty(PropertyName = "Pok\u00e9mon Center Uses")]
		public int PokemonCentersUsed;

		[JsonProperty(PropertyName = "Battles Fought (Total)")]
		public int BattlesFought;
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
