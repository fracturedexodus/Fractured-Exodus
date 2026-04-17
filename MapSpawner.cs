using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class MapSpawner
{
	public static void SetupSpaceBackground(Node parent, Vector2 screenSize)
	{
		TextureRect spaceBackgroundRect = new TextureRect();
		Texture2D bgTex = GD.Load<Texture2D>("res://space_bg.png"); 
		if (bgTex != null)
		{
			spaceBackgroundRect.Texture = bgTex;
			spaceBackgroundRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize; 
			spaceBackgroundRect.Size = screenSize; 
			spaceBackgroundRect.Modulate = new Color(0.6f, 0.6f, 0.7f, 1.0f); 
			parent.AddChild(spaceBackgroundRect);
		}
	}

	public static void GenerateGrid(int maxRadius, float hexSize, PackedScene hexScene, Node2D gridLayer, Dictionary<Vector2I, Node2D> hexGrid)
	{
		for (int q = -maxRadius; q <= maxRadius; q++)
		{
			int r1 = Mathf.Max(-maxRadius, -q - maxRadius);
			int r2 = Mathf.Min(maxRadius, -q + maxRadius);
			for (int r = r1; r <= r2; r++)
			{
				Vector2I hexCoord = new Vector2I(q, r);
				Vector2 worldPos = HexMath.HexToPixel(hexCoord, hexSize);
				Node2D hexTile = hexScene.Instantiate<Node2D>();
				hexTile.Position = worldPos;
				gridLayer.AddChild(hexTile);
				hexGrid.Add(hexCoord, hexTile);
			}
		}
	}

	public static bool IsHexEmpty(Vector2I hex, Dictionary<Vector2I, Node2D> hexGrid, Dictionary<Vector2I, MapEntity> hexContents)
	{
		if (!hexGrid.ContainsKey(hex)) return false; 
		if (hexContents.ContainsKey(hex))
		{
			string type = hexContents[hex].Type;
			if (type == "Planet" || type == "Base Planet (Player Start)" || type == "Celestial Body" || type == "Player Fleet" || type == "Enemy Fleet" || type == "StarGate")
				return false; 
		}
		return true;
	}

	private static int GetStableHash(string s)
	{
		if (string.IsNullOrEmpty(s)) return 0;
		unchecked
		{
			int hash = 23;
			foreach (char c in s) hash = hash * 31 + c;
			return hash;
		}
	}

	public static void PopulateMapFromMemory(
		GlobalData globalData, 
		int maxRadius, 
		float hexSize, 
		Dictionary<Vector2I, Node2D> hexGrid, 
		Dictionary<Vector2I, MapEntity> hexContents,
		Node2D entityLayer, 
		Node2D environmentLayer, 
		Node2D radiationLayer,
		HashSet<Vector2I> asteroidHexes,
		HashSet<Vector2I> radiationHexes)
	{
		Random rng = new Random();
		if (globalData != null && !string.IsNullOrEmpty(globalData.SavedSystem))
			rng = new Random(GetStableHash(globalData.SavedSystem)); 

		MapEntity starData = new MapEntity { Name = "Main Sequence Star", Type = "Celestial Body", Details = "Extreme Heat Signature" };
		if (globalData != null && !string.IsNullOrEmpty(globalData.SavedSystem))
			starData.Name = globalData.SavedSystem.ToUpper() + " PRIME";
			
		SpawnEntityAtHex(new Vector2I(0, 0), "res://YellowSUN.png", starData, 1.5f, hexSize, hexGrid, hexContents, entityLayer); 
		
		if (GodotObject.IsInstanceValid(starData.VisualSprite))
		{
			BattleVFX.AddSunVFX(starData.VisualSprite);
		}

		if (globalData == null || string.IsNullOrEmpty(globalData.SavedSystem)) return;

		if (!globalData.ExploredSystems.ContainsKey(globalData.SavedSystem))
		{
			SystemData newSys = new SystemData();
			newSys.SystemName = globalData.SavedSystem;
			
			int pCount = rng.Next(1, 6);
			if (globalData.CurrentSectorStars != null)
			{
				foreach (var star in globalData.CurrentSectorStars)
				{
					if (star.SystemName == globalData.SavedSystem)
					{
						pCount = star.PlanetCount;
						break;
					}
				}
			}

			string[] planetSuffixes = { "Prime", "Secundus", "Tertius", "Quartus", "Quintus", "Sextus", "Septimus", "Octavus" };

			for (int i = 0; i < pCount; i++)
			{
				PlanetData newP = new PlanetData();
				string suffix = i < planetSuffixes.Length ? planetSuffixes[i] : (i + 1).ToString();
				newP.Name = globalData.SavedSystem + " " + suffix;
				newP.TypeIndex = rng.Next(0, 6);
				newP.Scale = 0.4f + (float)rng.NextDouble() * 0.4f;
				newP.Habitability = "Unknown";
				newSys.Planets.Add(newP);
			}

			globalData.ExploredSystems[globalData.SavedSystem] = newSys;
		}

		SystemData currentSystem = globalData.ExploredSystems[globalData.SavedSystem];
		Vector2I basePlanetLocation = new Vector2I(2, -1); 
		
		int currentOrbitRing = 2; 
		foreach (PlanetData pData in currentSystem.Planets)
		{
			Vector2I spawnHex;
			if (pData.Position != Vector2.Zero)
			{
				spawnHex = new Vector2I((int)pData.Position.X, (int)pData.Position.Y);
			}
			else
			{
				spawnHex = FindEmptyHexInRing(currentOrbitRing, rng, hexGrid, hexContents);
				pData.Position = new Vector2(spawnHex.X, spawnHex.Y);
			}
			
			currentOrbitRing += 3; 
			string pTypeStr = GetPlanetTypeString(pData.TypeIndex);
			string pTex = GetTexturePathForType(pTypeStr);
			MapEntity planetEntity = new MapEntity { Name = pData.Name, Type = "Planet", Details = $"Biome Class: {pTypeStr.ToUpper()}\nHab: {pData.Habitability}" };
			SpawnEntityAtHex(spawnHex, pTex, planetEntity, pData.Scale, hexSize, hexGrid, hexContents, entityLayer);
			
			if (GodotObject.IsInstanceValid(planetEntity.VisualSprite))
			{
				BattleVFX.AddPlanetRotationVFX(planetEntity.VisualSprite, rng);
			}

			if (pData.Name == globalData.SavedPlanet) basePlanetLocation = spawnHex;
		}

		// --- PERSISTENT STARGATES ---
		// We no longer generate gates here! They are generated in GalacticMap and read from memory.
		foreach (Vector2I gateHex in currentSystem.StargateHexes)
		{
			MapEntity gateEntity = new MapEntity { Name = "Ancient StarGate", Type = "StarGate", Details = "Trans-dimensional warp gate connecting local star systems." };
			SpawnEntityAtHex(gateHex, "res://StarGate.png", gateEntity, 0.4f, hexSize, hexGrid, hexContents, entityLayer);
		}

		// --- PERSISTENT ASTEROIDS ---
		asteroidHexes.Clear();
		if (currentSystem.AsteroidHexes != null && currentSystem.AsteroidHexes.Count > 0)
		{
			foreach (Vector2I hex in currentSystem.AsteroidHexes)
			{
				asteroidHexes.Add(hex);
				BattleVFX.DrawAsteroidVisual(environmentLayer, hex, rng, hexSize, HexMath.HexToPixel(hex, hexSize));
			}
		}
		else 
		{
			int numAsteroidFields = rng.Next(0, 4); 
			for(int i = 0; i < numAsteroidFields; i++)
			{
				int fieldSize = rng.Next(5, 101); 
				Vector2I startHex = FindEmptyHexInRing(rng.Next(10, maxRadius), rng, hexGrid, hexContents);
				List<Vector2I> cluster = new List<Vector2I> { startHex };
				
				asteroidHexes.Add(startHex);
				BattleVFX.DrawAsteroidVisual(environmentLayer, startHex, rng, hexSize, HexMath.HexToPixel(startHex, hexSize));

				int attempts = 0;
				while (cluster.Count < fieldSize && attempts < 1000) 
				{
					Vector2I baseHex = cluster[rng.Next(cluster.Count)];
					Vector2I neighbor = baseHex + HexMath.Directions[rng.Next(6)];
					
					if (HexMath.HexDistance(neighbor, Vector2I.Zero) >= 10 && HexMath.HexDistance(neighbor, Vector2I.Zero) <= maxRadius)
					{
						if (!asteroidHexes.Contains(neighbor) && IsHexEmpty(neighbor, hexGrid, hexContents))
						{
							cluster.Add(neighbor);
							asteroidHexes.Add(neighbor);
							BattleVFX.DrawAsteroidVisual(environmentLayer, neighbor, rng, hexSize, HexMath.HexToPixel(neighbor, hexSize));
						}
					}
					attempts++;
				}
			}
		}

		// --- PERSISTENT RADIATION ---
		radiationHexes.Clear();
		if (currentSystem.RadiationHexes != null && currentSystem.RadiationHexes.Count > 0)
		{
			foreach (Vector2I hex in currentSystem.RadiationHexes)
			{
				radiationHexes.Add(hex);
				BattleVFX.DrawRadiationVisual(radiationLayer, hex, rng, hexSize, HexMath.HexToPixel(hex, hexSize));
			}
		}
		else
		{
			int numRadiationFields = rng.Next(0, 4); 
			for(int i = 0; i < numRadiationFields; i++)
			{
				int fieldSize = rng.Next(20, 80); 
				Vector2I startHex = FindEmptyHexInRing(rng.Next(10, maxRadius), rng, hexGrid, hexContents);
				List<Vector2I> cluster = new List<Vector2I> { startHex };
				
				radiationHexes.Add(startHex);
				BattleVFX.DrawRadiationVisual(radiationLayer, startHex, rng, hexSize, HexMath.HexToPixel(startHex, hexSize));

				int attempts = 0;
				while (cluster.Count < fieldSize && attempts < 1000) 
				{
					Vector2I baseHex = cluster[rng.Next(cluster.Count)];
					Vector2I neighbor = baseHex + HexMath.Directions[rng.Next(6)];
					
					if (HexMath.HexDistance(neighbor, Vector2I.Zero) <= maxRadius)
					{
						if (!radiationHexes.Contains(neighbor))
						{
							cluster.Add(neighbor);
							radiationHexes.Add(neighbor);
							BattleVFX.DrawRadiationVisual(radiationLayer, neighbor, rng, hexSize, HexMath.HexToPixel(neighbor, hexSize));
						}
					}
					attempts++;
				}
			}
		}

		bool arrivedViaJump = false;
		if (globalData != null) 
		{
			arrivedViaJump = globalData.JustJumped;
			if (arrivedViaJump)
			{
				if (currentSystem.StargateHexes.Count > 0)
				{
					basePlanetLocation = currentSystem.StargateHexes[rng.Next(currentSystem.StargateHexes.Count)];
				}
				else
				{
					basePlanetLocation = FindEmptyHexInRing(rng.Next(10, maxRadius - 5), rng, hexGrid, hexContents);
				}
				globalData.JustJumped = false;
			}
		}

		if (globalData.SavedFleetState != null && globalData.SavedFleetState.Count > 0)
		{
			int jumpSpawnOffset = 0; 
			foreach (var item in globalData.SavedFleetState)
			{
				var shipDict = (Godot.Collections.Dictionary)item;
				string shipName = (string)shipDict["Name"];
				
				(int range, int dmg) = Database.GetShipWeaponStats(shipName);
				
				Vector2I spawnPos = new Vector2I((int)shipDict["Q"], (int)shipDict["R"]);
				if (arrivedViaJump) 
				{
					spawnPos = basePlanetLocation + HexMath.Directions[jumpSpawnOffset % 6];
					jumpSpawnOffset++;
				}

				int actions = shipDict.ContainsKey("CurrentActions") ? (int)shipDict["CurrentActions"] : (int)shipDict["CurrentMovement"];
				int maxActs = shipDict.ContainsKey("MaxActions") ? (int)shipDict["MaxActions"] : (int)shipDict["MaxMovement"];

				MapEntity shipData = new MapEntity { 
					Name = shipName, Type = "Player Fleet", Details = "Status: Online",
					MaxActions = maxActs, CurrentActions = actions,
					AttackRange = range, AttackDamage = dmg,
					MaxHP = (int)shipDict["MaxHP"], CurrentHP = (int)shipDict["CurrentHP"],
					MaxShields = (int)shipDict["MaxShields"], CurrentShields = (int)shipDict["CurrentShields"],
					InitiativeBonus = Database.GetShipInitiativeBonus(shipName),
					BaseRotationOffset = Database.GetShipRotationOffset(shipName),
					CurrentInitiativeRoll = shipDict.ContainsKey("CurrentInitiativeRoll") ? (int)shipDict["CurrentInitiativeRoll"] : 0
				};
				
				SpawnEntityAtHex(spawnPos, Database.GetShipTexturePath(shipName), shipData, 0.2f, hexSize, hexGrid, hexContents, entityLayer); 
			}
		}
		else if (globalData.SelectedPlayerFleet != null && globalData.SelectedPlayerFleet.Count > 0)
		{
			int currentDirIndex = 0;
			foreach (string shipName in globalData.SelectedPlayerFleet)
			{
				while (currentDirIndex < 6)
				{
					Vector2I spawnPos = basePlanetLocation + HexMath.Directions[currentDirIndex];
					currentDirIndex++;
					if (hexGrid.ContainsKey(spawnPos) && !hexContents.ContainsKey(spawnPos))
					{
						int shipBaseActionPoints = Database.GetShipBaseActions(shipName); 
						(int hp, int shields) = Database.GetShipCombatStats(shipName);
						(int range, int dmg) = Database.GetShipWeaponStats(shipName);

						MapEntity shipData = new MapEntity { 
							Name = shipName, Type = "Player Fleet", Details = "Status: Online",
							MaxActions = shipBaseActionPoints, CurrentActions = shipBaseActionPoints,
							AttackRange = range, AttackDamage = dmg,
							MaxHP = hp, CurrentHP = hp, MaxShields = shields, CurrentShields = shields,
							InitiativeBonus = Database.GetShipInitiativeBonus(shipName),
							BaseRotationOffset = Database.GetShipRotationOffset(shipName)
						};
						
						SpawnEntityAtHex(spawnPos, Database.GetShipTexturePath(shipName), shipData, 0.2f, hexSize, hexGrid, hexContents, entityLayer); 
						break; 
					}
				}
			}
		}

		if (currentSystem.HasBeenVisited && currentSystem.EnemyFleets != null && currentSystem.EnemyFleets.Count > 0)
		{
			foreach (var item in currentSystem.EnemyFleets)
			{
				var shipDict = (Godot.Collections.Dictionary)item;
				string shipName = (string)shipDict["Name"];
				Vector2I spawnPos = new Vector2I((int)shipDict["Q"], (int)shipDict["R"]);
				
				(int range, int dmg) = Database.GetShipWeaponStats(shipName);

				int actions = shipDict.ContainsKey("CurrentActions") ? (int)shipDict["CurrentActions"] : (int)shipDict["CurrentMovement"];
				int maxActs = shipDict.ContainsKey("MaxActions") ? (int)shipDict["MaxActions"] : (int)shipDict["MaxMovement"];

				MapEntity shipData = new MapEntity { 
					Name = shipName, Type = "Enemy Fleet", Details = "Status: Hostile Target",
					MaxActions = maxActs, CurrentActions = actions,
					AttackRange = range, AttackDamage = dmg,
					MaxHP = (int)shipDict["MaxHP"], CurrentHP = (int)shipDict["CurrentHP"],
					MaxShields = (int)shipDict["MaxShields"], CurrentShields = (int)shipDict["CurrentShields"],
					InitiativeBonus = Database.GetShipInitiativeBonus(shipName),
					BaseRotationOffset = Database.GetShipRotationOffset(shipName),
					CurrentInitiativeRoll = shipDict.ContainsKey("CurrentInitiativeRoll") ? (int)shipDict["CurrentInitiativeRoll"] : 0
				};
				
				SpawnEntityAtHex(spawnPos, Database.GetShipTexturePath(shipName), shipData, 0.2f, hexSize, hexGrid, hexContents, entityLayer); 
			}
		}
		else if (!currentSystem.HasBeenVisited)
		{
			int enemyFleetCount = rng.Next(1, 6); 
			var savedEnemyArray = new Godot.Collections.Array();

			for (int fleet = 0; fleet < enemyFleetCount; fleet++)
			{
				Vector2I fleetBaseLocation = FindEmptyHexInRing(rng.Next(10, maxRadius - 2), rng, hexGrid, hexContents);
				int shipsInThisFleet = rng.Next(1, 4); 
				int enemyDirIndex = 0;

				for (int i = 0; i < shipsInThisFleet; i++)
				{
					string enemyName = Database.EnemyShipTypes[rng.Next(Database.EnemyShipTypes.Length)];
					while (enemyDirIndex < 18) 
					{
						int ring = (enemyDirIndex / 6) + 1;
						Vector2I spawnPos = fleetBaseLocation + HexMath.Directions[enemyDirIndex % 6] * ring;
						enemyDirIndex++;
						if (hexGrid.ContainsKey(spawnPos) && !hexContents.ContainsKey(spawnPos))
						{
							int shipBaseActionPoints = Database.GetShipBaseActions(enemyName); 
							(int hp, int shields) = Database.GetShipCombatStats(enemyName);
							(int range, int dmg) = Database.GetShipWeaponStats(enemyName);

							MapEntity shipData = new MapEntity { 
								Name = enemyName, Type = "Enemy Fleet", Details = "Status: Hostile Target",
								MaxActions = shipBaseActionPoints, CurrentActions = shipBaseActionPoints,
								AttackRange = range, AttackDamage = dmg,
								MaxHP = hp, CurrentHP = hp, MaxShields = shields, CurrentShields = shields,
								InitiativeBonus = Database.GetShipInitiativeBonus(enemyName),
								BaseRotationOffset = Database.GetShipRotationOffset(enemyName)
							};
							
							SpawnEntityAtHex(spawnPos, Database.GetShipTexturePath(enemyName), shipData, 0.2f, hexSize, hexGrid, hexContents, entityLayer); 

							var shipDict = new Godot.Collections.Dictionary<string, Variant>();
							shipDict["Name"] = enemyName; shipDict["Q"] = spawnPos.X; shipDict["R"] = spawnPos.Y;
							shipDict["CurrentHP"] = hp; shipDict["MaxHP"] = hp; shipDict["CurrentShields"] = shields; shipDict["MaxShields"] = shields;
							shipDict["MaxActions"] = shipBaseActionPoints; shipDict["CurrentActions"] = shipBaseActionPoints;
							savedEnemyArray.Add(shipDict);
							break; 
						}
					}
				}
			}
			currentSystem.EnemyFleets = savedEnemyArray;
			currentSystem.HasBeenVisited = true; 
		}
	}

	public static Vector2I FindEmptyHexInRing(int radius, Random rng, Dictionary<Vector2I, Node2D> hexGrid, Dictionary<Vector2I, MapEntity> hexContents)
	{
		List<Vector2I> ringHexes = new List<Vector2I>();
		Vector2I currentHex = new Vector2I(0, -radius);
		foreach (Vector2I dir in HexMath.Directions)
		{
			for (int i = 0; i < radius; i++)
			{
				ringHexes.Add(currentHex);
				currentHex += dir;
			}
		}
		ShuffleList(ringHexes, rng);
		foreach (var hex in ringHexes) if (hexGrid.ContainsKey(hex) && !hexContents.ContainsKey(hex)) return hex;
		return new Vector2I(radius, 0); 
	}

	private static void ShuffleList<T>(List<T> list, Random rng)  
	{  
		int n = list.Count;  
		while (n > 1) { n--; int k = rng.Next(n + 1); T value = list[k]; list[k] = list[n]; list[n] = value; }  
	}

	public static void SpawnEntityAtHex(Vector2I hexCoord, string texturePath, MapEntity entityData, float scale, float hexSize, Dictionary<Vector2I, Node2D> hexGrid, Dictionary<Vector2I, MapEntity> hexContents, Node2D entityLayer)
	{
		if (!hexGrid.ContainsKey(hexCoord)) return;
		Sprite2D entitySprite = new Sprite2D();
		Texture2D tex = GD.Load<Texture2D>(texturePath);
		if (tex != null) entitySprite.Texture = tex;
		entitySprite.Scale = new Vector2(scale, scale);
		entitySprite.Position = HexMath.HexToPixel(hexCoord, hexSize);
		entityLayer.AddChild(entitySprite);
		entityData.VisualSprite = entitySprite;
		hexContents[hexCoord] = entityData;
	}

	public static string GetPlanetTypeString(int typeIndex)
	{
		string[] types = { "Terra", "Arid", "Ocean", "Toxic", "Frozen", "Lava" };
		if (typeIndex >= 0 && typeIndex < types.Length) return types[typeIndex];
		return "Terra";
	}

	public static string GetTexturePathForType(string type)
	{
		if (string.IsNullOrEmpty(type)) return "res://Planets/terra_planet.png";
		switch (type.ToUpper())
		{
			case "TERRA": return "res://Planets/terra_planet.png";
			case "ARID": return "res://Planets/arid_planet.png";
			case "OCEAN": return "res://Planets/ocean_planet.png";
			case "TOXIC": return "res://Planets/toxic_planet.png";
			case "FROZEN": return "res://Planets/frozen_planet.png";
			case "LAVA": return "res://Planets/lava_planet.png";
			default: return "res://Planets/terra_planet.png"; 
		}
	}
}
