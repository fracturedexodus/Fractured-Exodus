using Godot;
using System;
using System.Linq;

public static class BattleVFX
{
	public static void DrawLaserBeam(Node2D parentLayer, Vector2 startPos, Vector2 endPos, string attackerType)
	{
		if (!IsNodeAlive(parentLayer)) return;

		Line2D laser = new Line2D();
		laser.AddPoint(startPos);
		laser.AddPoint(endPos);
		laser.Width = 4.0f;
		
		if (attackerType == GameConstants.EntityTypes.PlayerFleet) laser.DefaultColor = new Color(0.2f, 1f, 0.2f, 1f);
		else laser.DefaultColor = new Color(1f, 0.2f, 0.2f, 1f); 
		
		parentLayer.AddChild(laser);

		Tween tween = laser.CreateTween();
		tween.TweenProperty(laser, "modulate", new Color(1, 1, 1, 0), 0.4f);
		tween.TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(laser)) laser.QueueFree();
		}));
	}

	public static void DrawMissileStrike(Node2D parentLayer, Vector2 startPos, Vector2 endPos, string attackerType)
	{
		if (!IsNodeAlive(parentLayer)) return;

		Node2D missileRoot = new Node2D();
		missileRoot.Position = startPos;
		parentLayer.AddChild(missileRoot);

		Line2D contrail = new Line2D();
		contrail.Width = 3.0f;
		contrail.DefaultColor = new Color(1f, 0.7f, 0.2f, 0.85f);
		contrail.AddPoint(Vector2.Zero);
		contrail.AddPoint(new Vector2(-24f, 0f));
		missileRoot.AddChild(contrail);

		Polygon2D missileBody = new Polygon2D();
		missileBody.Polygon = new[]
		{
			new Vector2(16f, 0f),
			new Vector2(-8f, -6f),
			new Vector2(-14f, 0f),
			new Vector2(-8f, 6f)
		};
		missileBody.Color = attackerType == GameConstants.EntityTypes.PlayerFleet
			? new Color(1f, 0.85f, 0.25f, 1f)
			: new Color(1f, 0.45f, 0.2f, 1f);
		missileRoot.AddChild(missileBody);

		Polygon2D engineFlare = new Polygon2D();
		engineFlare.Polygon = new[]
		{
			new Vector2(-14f, 0f),
			new Vector2(-26f, -4f),
			new Vector2(-32f, 0f),
			new Vector2(-26f, 4f)
		};
		engineFlare.Color = new Color(1f, 0.95f, 0.6f, 0.9f);
		missileRoot.AddChild(engineFlare);

		Vector2 travel = endPos - startPos;
		missileRoot.Rotation = travel.Angle();

		Tween tween = missileRoot.CreateTween();
		tween.TweenProperty(missileRoot, "position", endPos, 0.45f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		tween.Parallel().TweenProperty(engineFlare, "modulate:a", 0.35f, 0.45f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		tween.TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(missileRoot)) missileRoot.QueueFree();
		}));
	}

	public static void DrawExplosion(Node2D parentLayer, Vector2 pos, float hexSize)
	{
		if (!IsNodeAlive(parentLayer)) return;

		Polygon2D shockwave = new Polygon2D();
		Vector2[] points = new Vector2[32];
		for (int i = 0; i < 32; i++)
		{
			float angle = (i / 32f) * Mathf.Pi * 2f;
			points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * hexSize * 0.5f;
		}
		shockwave.Polygon = points;
		shockwave.Color = new Color(1f, 1f, 1f, 0.8f); 
		shockwave.Position = pos;
		parentLayer.AddChild(shockwave);

		Tween flashTween = shockwave.CreateTween();
		flashTween.TweenProperty(shockwave, "scale", new Vector2(3.0f, 3.0f), 0.8f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		flashTween.Parallel().TweenProperty(shockwave, "color", new Color(1f, 0.4f, 0f, 0f), 0.8f); 
		flashTween.TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(shockwave)) shockwave.QueueFree();
		}));

		CpuParticles2D particles = new CpuParticles2D();
		particles.Position = pos;
		particles.Emitting = false;
		particles.OneShot = true;
		particles.Explosiveness = 0.6f; 
		particles.Lifetime = 2.5f; 
		particles.Amount = 60; 
		particles.Spread = 180f;
		particles.Gravity = Vector2.Zero; 
		particles.InitialVelocityMin = 20f; 
		particles.InitialVelocityMax = 80f; 
		particles.ScaleAmountMin = 8f;
		particles.ScaleAmountMax = 24f;
		
		Gradient grad = new Gradient();
		grad.Offsets = new float[] { 0.0f, 0.1f, 0.3f, 0.6f, 1.0f };
		grad.Colors = new Color[] {
			new Color(1f, 1f, 1f, 1f),       
			new Color(1f, 0.9f, 0.2f, 1f),   
			new Color(1f, 0.4f, 0f, 1f),     
			new Color(0.2f, 0.2f, 0.2f, 1f), 
			new Color(0.2f, 0.2f, 0.2f, 0f)  
		};
		particles.ColorRamp = grad;

		Curve sizeCurve = new Curve();
		sizeCurve.AddPoint(new Vector2(0f, 1f));
		sizeCurve.AddPoint(new Vector2(1f, 0f));
		particles.ScaleAmountCurve = sizeCurve;

		parentLayer.AddChild(particles);
		particles.Emitting = true; 

		SceneTree tree = parentLayer.GetTree();
		if (tree == null) return;

		tree.CreateTimer(3.0f).Timeout += () =>
		{
			if (GodotObject.IsInstanceValid(particles)) particles.QueueFree();
		};
	}

	private static bool IsNodeAlive(Node node)
	{
		return node != null && GodotObject.IsInstanceValid(node);
	}

	public static void AddSunVFX(Sprite2D sunSprite)
	{
		Sprite2D glow = new Sprite2D();
		glow.Texture = sunSprite.Texture; 
		glow.Scale = new Vector2(1.2f, 1.2f); 
		glow.Modulate = new Color(1f, 0.8f, 0.2f, 0.4f); 
		glow.ShowBehindParent = true;
		
		CanvasItemMaterial mat = new CanvasItemMaterial();
		mat.BlendMode = CanvasItemMaterial.BlendModeEnum.Add;
		glow.Material = mat;
		sunSprite.AddChild(glow);

		Tween pulseTween = sunSprite.CreateTween().SetLoops(); 
		pulseTween.TweenProperty(glow, "scale", new Vector2(1.3f, 1.3f), 2.0f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		pulseTween.TweenProperty(glow, "scale", new Vector2(1.15f, 1.15f), 2.0f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

		CpuParticles2D flares = new CpuParticles2D();
		flares.Amount = 40;
		flares.Lifetime = 3.0f;
		flares.SpeedScale = 0.5f;
		flares.Explosiveness = 0.1f;
		flares.Randomness = 0.5f;
		flares.ShowBehindParent = true;

		flares.EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere;
		if (sunSprite.Texture != null)
		{
			flares.EmissionSphereRadius = (sunSprite.Texture.GetSize().X / 2f) * 0.9f; 
		}
		
		flares.Direction = Vector2.Zero; 
		flares.Spread = 180f;
		flares.Gravity = Vector2.Zero;
		flares.InitialVelocityMin = 20f;
		flares.InitialVelocityMax = 60f;
		flares.ScaleAmountMin = 4f;
		flares.ScaleAmountMax = 12f;

		Gradient colorGrad = new Gradient();
		colorGrad.Offsets = new float[] { 0.0f, 0.2f, 0.6f, 1.0f };
		colorGrad.Colors = new Color[] {
			new Color(1f, 1f, 0.8f, 1f),     
			new Color(1f, 0.8f, 0.1f, 0.9f), 
			new Color(1f, 0.4f, 0f, 0.6f),   
			new Color(0.8f, 0.1f, 0f, 0f)    
		};
		flares.ColorRamp = colorGrad;

		Curve sizeCurve = new Curve();
		sizeCurve.AddPoint(new Vector2(0f, 1f));
		sizeCurve.AddPoint(new Vector2(1f, 0f));
		flares.ScaleAmountCurve = sizeCurve;

		sunSprite.AddChild(flares);

		Tween rotTween = sunSprite.CreateTween().SetLoops();
		rotTween.TweenProperty(sunSprite, "rotation", Mathf.Pi * 2, 60.0f).AsRelative();
	}

	public static void AddPlanetRotationVFX(Sprite2D planetSprite, Random rng)
	{
		float rotDuration = rng.Next(40, 120); 
		float rotDir = rng.Next(0, 2) == 0 ? 1f : -1f; 

		Tween rotTween = planetSprite.CreateTween().SetLoops();
		rotTween.TweenProperty(planetSprite, "rotation", Mathf.Pi * 2 * rotDir, rotDuration).AsRelative();
	}

	public static void DrawAsteroidVisual(Node2D environmentLayer, Vector2I hexCoord, Random rng, float hexSize, Vector2 pixelPos)
	{
		Polygon2D rock = new Polygon2D();
		int pointsCount = rng.Next(5, 9);
		Vector2[] pts = new Vector2[pointsCount];
		
		for(int i = 0; i < pointsCount; i++) 
		{
			float angle = (i / (float)pointsCount) * Mathf.Pi * 2;
			float rad = hexSize * 0.35f * (0.7f + (float)rng.NextDouble() * 0.6f);
			pts[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * rad;
		}
		
		rock.Polygon = pts;
		rock.Color = new Color(0.4f, 0.4f, 0.4f, 1f);
		
		Line2D outline = new Line2D();
		outline.Points = pts.Append(pts[0]).ToArray();
		outline.Width = 2f;
		outline.DefaultColor = new Color(0.2f, 0.2f, 0.2f, 1f);
		rock.AddChild(outline);

		rock.Position = pixelPos;
		rock.Rotation = (float)rng.NextDouble() * Mathf.Pi;
		
		rock.SetMeta("is_asteroid", true);
		rock.SetMeta("asteroid_hex_q", hexCoord.X);
		rock.SetMeta("asteroid_hex_r", hexCoord.Y);
		rock.SetMeta("spin_speed", (float)(rng.NextDouble() * 1.5 - 0.75));
		
		environmentLayer.AddChild(rock);
	}

	public static void DrawRadiationVisual(Node2D radiationLayer, Vector2I hexCoord, Random rng, float hexSize, Vector2 pixelPos)
	{
		Polygon2D cloud = new Polygon2D();
		Vector2[] points = new Vector2[6];
		for (int i = 0; i < 6; i++)
		{
			float angle_deg = 60 * i - 30;
			float angle_rad = Mathf.DegToRad(angle_deg);
			points[i] = new Vector2(hexSize * 1.05f * Mathf.Cos(angle_rad), hexSize * 1.05f * Mathf.Sin(angle_rad));
		}
		cloud.Polygon = points;
		
		float g = 0.8f + (float)rng.NextDouble() * 0.2f;
		cloud.Color = new Color(0.2f, g, 0.1f, 0.15f); 
		
		CanvasItemMaterial mat = new CanvasItemMaterial();
		mat.BlendMode = CanvasItemMaterial.BlendModeEnum.Add;
		cloud.Material = mat;

		cloud.Position = pixelPos;
		radiationLayer.AddChild(cloud);
		
		Tween pulseTween = cloud.CreateTween().SetLoops();
		pulseTween.TweenProperty(cloud, "modulate:a", 0.05f, rng.Next(2, 5)).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		pulseTween.TweenProperty(cloud, "modulate:a", 1.0f, rng.Next(2, 5)).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
	}
}
