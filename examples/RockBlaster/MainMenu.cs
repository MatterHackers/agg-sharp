using Gaming.Game;
using Gaming.Graphics;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using System;
using System.Diagnostics;

namespace RockBlaster
{
	public class TimedInterpolator
	{
		private static Stopwatch runningTime = new Stopwatch();

		public enum Repeate { NONE, LOOP, PINGPONG };

		public enum InterpolationType { LINEAR, EASE_IN, EASE_OUT, EASE_IN_OUT };

		private double timeStartedAt;
		private double numSeconds;
		private double startValue;
		private double endValue;
		private double distance;
		private Repeate repeateType = Repeate.NONE;
		private InterpolationType interpolationType = InterpolationType.LINEAR;

		private bool haveStarted = false;
		private bool done = false;

		public TimedInterpolator()
		{
			if (!runningTime.IsRunning)
			{
				runningTime.Start();
			}
		}

		public TimedInterpolator(double in_numSeconds, double in_startValue, double in_endValue, Repeate in_repeateType, InterpolationType in_interpolationType)
			: this()
		{
			numSeconds = in_numSeconds;
			startValue = in_startValue;
			endValue = in_endValue;
			distance = endValue - startValue;
			repeateType = in_repeateType;
			interpolationType = in_interpolationType;
		}

		public void Reset()
		{
			done = false;
			haveStarted = false;
		}

		public double GetInterpolatedValue(double compleatedRatio0To1)
		{
			switch (interpolationType)
			{
				case InterpolationType.LINEAR:
					return startValue + distance * compleatedRatio0To1;

				case InterpolationType.EASE_IN:
					return distance * Math.Pow(compleatedRatio0To1, 3) + startValue;

				case InterpolationType.EASE_OUT:
					return distance * (Math.Pow(compleatedRatio0To1 - 1, 3) + 1) + startValue;

				case InterpolationType.EASE_IN_OUT:
					if (compleatedRatio0To1 < .5)
					{
						return distance / 2 * Math.Pow(compleatedRatio0To1 * 2, 3) + startValue;
					}
					else
					{
						return distance / 2 * (Math.Pow(compleatedRatio0To1 * 2 - 2, 3) + 2) + startValue;
					}

				default:
					throw new NotImplementedException();
			}
		}

		public double Read()
		{
			if (done)
			{
				return endValue;
			}

			if (!haveStarted)
			{
				timeStartedAt = runningTime.Elapsed.TotalSeconds;
				haveStarted = true;
			}
			double timePassed = runningTime.Elapsed.TotalSeconds - timeStartedAt;
			double compleatedRatio = timePassed / numSeconds;

			switch (repeateType)
			{
				case Repeate.NONE:
					if (compleatedRatio > 1)
					{
						done = true;
						return endValue;
					}
					return GetInterpolatedValue(compleatedRatio);

				case Repeate.LOOP:
					{
						int compleatedRatioInt = (int)compleatedRatio;
						compleatedRatio = compleatedRatio - compleatedRatioInt;
						return GetInterpolatedValue(compleatedRatio);
					}

				case Repeate.PINGPONG:
					{
						int compleatedRatioInt = (int)compleatedRatio;
						compleatedRatio = compleatedRatio - compleatedRatioInt;
						if ((compleatedRatioInt & 1) == 1)
						{
							return GetInterpolatedValue(1 - compleatedRatio);
						}
						else
						{
							return GetInterpolatedValue(compleatedRatio);
						}
					}

				default:
					throw new NotImplementedException();
			}
		}
	};

	/// <summary>
	/// Description of MainMenu.
	/// </summary>
	public class MainMenu : GuiWidget
	{
		public delegate void StartGameEventHandler(GuiWidget button);

		public event StartGameEventHandler StartGame;

		public delegate void ShowCreditsEventHandler(GuiWidget button);

		public event ShowCreditsEventHandler ShowCredits;

		public delegate void ExitGameEventHandler(GuiWidget button);

		public event ExitGameEventHandler ExitGame;

		private TimedInterpolator shipRatio = new TimedInterpolator(1, 1.0 / 8.0, 3.0 / 8.0, TimedInterpolator.Repeate.PINGPONG, TimedInterpolator.InterpolationType.EASE_IN_OUT);
		private TimedInterpolator planetRatio = new TimedInterpolator(.5, 0, 1, TimedInterpolator.Repeate.LOOP, TimedInterpolator.InterpolationType.LINEAR);

		public MainMenu(RectangleDouble bounds)
		{
			BoundsRelativeToParent = bounds;

			ImageSequence startButtonSequence = (ImageSequence)DataAssetCache.Instance.GetAsset(typeof(ImageSequence), "MainMenuStartButton");
			Button StartGameButton = new Button(400, 310, new ButtonViewThreeImage(startButtonSequence.GetImageByIndex(0), startButtonSequence.GetImageByIndex(1), startButtonSequence.GetImageByIndex(2)));
			AddChild(StartGameButton);
			StartGameButton.Click += OnStartGameButton;

			ImageSequence creditsButtonSequence = (ImageSequence)DataAssetCache.Instance.GetAsset(typeof(ImageSequence), "MainMenuCreditsButton");
			Button creditsGameButton = new Button(400, 230, new ButtonViewThreeImage(creditsButtonSequence.GetImageByIndex(0), creditsButtonSequence.GetImageByIndex(1), creditsButtonSequence.GetImageByIndex(2)));
			AddChild(creditsGameButton);
			creditsGameButton.Click += OnShowCreditsButton;

			ImageSequence exitButtonSequence = (ImageSequence)DataAssetCache.Instance.GetAsset(typeof(ImageSequence), "MainMenuExitButton");
			Button exitGameButton = new Button(400, 170, new ButtonViewThreeImage(exitButtonSequence.GetImageByIndex(0), exitButtonSequence.GetImageByIndex(1), exitButtonSequence.GetImageByIndex(2)));
			AddChild(exitGameButton);
			exitGameButton.Click += OnExitGameButton;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			ImageSequence menuBackground = (ImageSequence)DataAssetCache.Instance.GetAsset(typeof(ImageSequence), "MainMenuBackground");
			graphics2D.Render(menuBackground.GetImageByIndex(0), 0, 0);

			ImageSequence planetOnMenu = (ImageSequence)DataAssetCache.Instance.GetAsset(typeof(ImageSequence), "PlanetOnMenu");
			graphics2D.Render(planetOnMenu.GetImageByRatio(planetRatio.Read()), 620, 360);

			ImageSequence shipOnMenu = (ImageSequence)DataAssetCache.Instance.GetAsset(typeof(ImageSequence), "Player1Ship");

			int numFrames = shipOnMenu.NumFrames;
			double animationRatio0to1 = shipRatio.Read();
			double curFrameDouble = (numFrames - 1) * animationRatio0to1;
			int curFrameInt = shipOnMenu.GetFrameIndexByRatio(animationRatio0to1);
			double curFrameRemainder = curFrameDouble - curFrameInt;
			double anglePerFrame = MathHelper.Tau / numFrames;
			double angleForThisFrame = curFrameRemainder * anglePerFrame;

			graphics2D.Render(shipOnMenu.GetImageByIndex(curFrameInt), 177, 156, angleForThisFrame, 1, 1);

			base.OnDraw(graphics2D);
		}

		private void OnStartGameButton(object sender, EventArgs mouseEvent)
		{
			if (StartGame != null)
			{
				StartGame(this);
			}
		}

		private void OnShowCreditsButton(object sender, EventArgs mouseEent)
		{
			if (ShowCredits != null)
			{
				ShowCredits(this);
			}
		}

		private void OnExitGameButton(object sender, EventArgs mouseEvent)
		{
			if (ExitGame != null)
			{
				ExitGame(this);
			}
		}
	}
}