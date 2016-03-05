﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Urho;
using Urho.Actions;
using Urho.Gui;
using Urho.Shapes;

namespace FormsSample
{
	public class Charts : Application
	{
		bool movementsEnabled;
		Node plotNode;
		Camera camera;
		Octree octree;
		List<Bar> bars;
		private Node cameraNode;

		public Charts(ApplicationOptions options = null) : base(SetOptions(options)) {}

		public Bar SelectedBar { get; private set; }

		public IEnumerable<Bar> Bars => bars; 

		private static ApplicationOptions SetOptions(ApplicationOptions options)
		{
			options.TouchEmulation = true;
			return options;
		}

		protected override async void Start()
		{
			base.Start();
			Input.SubscribeToKeyDown(k => { if (k.Key == Key.Esc) Engine.Exit(); });
			Input.SubscribeToTouchEnd(OnTouched);

			// 3D scene with Octree
			var scene = new Scene(Context);
			octree = scene.CreateComponent<Octree>();

			// Camera
			cameraNode = scene.CreateChild(name: "camera");
			cameraNode.Position = new Vector3(5, 7, 5);
			cameraNode.Rotation = new Quaternion(-0.121f, 0.878f, -0.305f, -0.35f);
			camera = cameraNode.CreateComponent<Camera>();

			// Light
			Node lightNode = cameraNode.CreateChild(name: "light");
			var light = lightNode.CreateComponent<Light>();
			light.LightType = LightType.Point;
			light.Range = 100;
			light.Brightness = 1.3f;

			// Viewport
			Renderer.SetViewport(0, new Viewport(Context, scene, camera, null));

			plotNode = scene.CreateChild();
			var baseNode = plotNode.CreateChild().CreateChild();
			var plane = baseNode.CreateComponent<StaticModel>();
			plane.Model = ResourceCache.GetModel("Models/Plane.mdl");

			int size = 3;
			baseNode.Scale = new Vector3(size * 1.5f, 1, size * 1.5f);
			bars = new List<Bar>(size * size);
			for (var i = 0f; i < size * 1.5f; i += 1.5f)
			{
				for (var j = 0f; j < size * 1.5f; j += 1.5f)
				{
					var boxNode = plotNode.CreateChild();
					boxNode.Position = new Vector3(size / 2f - i, 0, size / 2f - j);
					var box = new Bar(new Color(RandomHelper.NextRandom(), RandomHelper.NextRandom(), RandomHelper.NextRandom(), 0.9f));
					boxNode.AddComponent(box);
					box.SetValueWithAnimation((Math.Abs(i) + Math.Abs(j) + 1) / 2f);
					bars.Add(box);
				}
			}
			SelectedBar = bars.First();
			SelectedBar.Select();
			await plotNode.RunActionsAsync(new EaseBackOut(new RotateBy(2f, 0, 360, 0)));
			movementsEnabled = true;
		}

		private void OnTouched(TouchEndEventArgs e)
		{
			Ray cameraRay = camera.GetScreenRay((float)e.X / Graphics.Width, (float)e.Y / Graphics.Height);
			var results = octree.RaycastSingle(cameraRay, RayQueryLevel.Triangle, 100, DrawableFlags.Geometry);
			if (results != null && results.Any())
			{
				var bar = results[0].Node?.Parent?.GetComponent<Bar>();
				if (SelectedBar != bar)
				{
					SelectedBar?.Deselect();
					SelectedBar = bar;
					SelectedBar?.Select();
				}
			}
		}

		protected override void OnUpdate(float timeStep)
		{
			if (Input.NumTouches == 1 && movementsEnabled)
			{
				var touch = Input.GetTouch(0);
				plotNode.Rotate(new Quaternion(0, -touch.Delta.X, 0), TransformSpace.Local);
			}
			base.OnUpdate(timeStep);
		}

		public void Rotate(float toValue)
		{
			plotNode.Rotate(new Quaternion(0, toValue, 0), TransformSpace.Local);
		}
	}

	public class Bar : Component
	{
		Node barNode;
		Node textNode;
		Text3D text3D;
		Color color;

		public float Value
		{
			get { return barNode.Scale.Y; }
			set { barNode.Scale = new Vector3(1, value < 0.3f ? 0.3f : value, 1); }
		}

		public void SetValueWithAnimation(float value) => barNode.RunActionsAsync(new EaseBackOut(new ScaleTo(3f, 1, value, 1)));

		public Bar(Color color)
		{
			this.color = color;
			ReceiveSceneUpdates = true;
		}

		public override void OnAttachedToNode(Node node)
		{
			barNode = node.CreateChild();
			barNode.Scale = new Vector3(1, 0, 1); //means zero height
			var box = barNode.CreateComponent<Box>();
			box.Color = color;

			textNode = node.CreateChild();
			textNode.Rotate(new Quaternion(0, 180, 0), TransformSpace.World);
			textNode.Position = new Vector3(0, 10, 0);
			text3D = textNode.CreateComponent<Text3D>();
			text3D.SetFont(Application.ResourceCache.GetFont("Fonts/Anonymous Pro.ttf"), 60);
			text3D.TextEffect = TextEffect.None;
			//textNode.LookAt() //Look at camera

			base.OnAttachedToNode(node);
		}

		protected override void OnUpdate(float timeStep)
		{
			var pos = barNode.Position;
			var scale = barNode.Scale;
			barNode.Position = new Vector3(pos.X, scale.Y / 2f, pos.Z);
			textNode.Position = new Vector3(0.5f, scale.Y + 0.2f, 0);
			text3D.Text = Math.Round(scale.Y, 1).ToString(CultureInfo.InvariantCulture);
		}

		public void Deselect()
		{
			barNode.RemoveAllActions();//TODO: remove only "selection" action
			barNode.RunActionsAsync(new EaseBackOut(new TintTo(1f, color.R, color.G, color.B)));
		}

		public void Select()
		{
			Selected?.Invoke(this);
			// "blinking" animation
			barNode.RunActionsAsync(new RepeatForever(new TintTo(0.3f, 1f, 1f, 1f), new TintTo(0.3f, color.R, color.G, color.B)));
		}

		public event Action<Bar> Selected;
	}
}
