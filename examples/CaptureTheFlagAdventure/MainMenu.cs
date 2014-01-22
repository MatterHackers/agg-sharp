using System;
using System.Collections.Generic;
using AGG;
using AGG.Image;
using AGG.VertexSource;
using AGG.UI;

using AGG.Transform;

using Gaming.Math;
using Gaming.Game;
using Gaming.Graphics;

namespace CTFA
{
    public class TimedInterpolator
    {
        private static bool runningTimeStarted;
        private static System.Diagnostics.Stopwatch runningTime = new System.Diagnostics.Stopwatch();

        public enum Repeate { NONE, LOOP, PINGPONG };
        public enum InterpolationType { LINEAR, EASE_IN, EASE_OUT, EASE_IN_OUT };

        double timeStartedAt;
        double numSeconds;
        double startValue;
        double endValue;
        double distance;
        Repeate repeateType = Repeate.NONE;
        InterpolationType interpolationType = InterpolationType.LINEAR;

        bool haveStarted = false;
        bool done = false;
        int direction;

        public TimedInterpolator()
        {
        }

        public TimedInterpolator(double in_numSeconds, double in_startValue, double in_endValue, Repeate in_repeateType, InterpolationType in_interpolationType)
        {
            if (!runningTimeStarted)
            {
                runningTime.Start();
            }
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
            switch(interpolationType)
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
	public class MainMenu : GUIWidget
	{
		public delegate void StartGameEventHandler(GUIWidget button);
		public event StartGameEventHandler StartGame;

		public delegate void ShowCreditsEventHandler(GUIWidget button);
		public event ShowCreditsEventHandler ShowCredits;

		public delegate void ExitGameEventHandler(GUIWidget button);
		public event ExitGameEventHandler ExitGame;

        TimedInterpolator shipRatio = new TimedInterpolator(1, 1.0/8.0, 3.0/8.0, TimedInterpolator.Repeate.PINGPONG, TimedInterpolator.InterpolationType.EASE_IN_OUT);
        TimedInterpolator planetRatio = new TimedInterpolator(.5, 0, 1, TimedInterpolator.Repeate.LOOP, TimedInterpolator.InterpolationType.LINEAR);

		public MainMenu(rect_d bounds)
		{
            Bounds = bounds;

            ImageSequence startButtonSequence = (ImageSequence)DataAssetCache.Instance.GetAsset(typeof(ImageSequence), "MainMenuStartButton");
            ButtonWidget StartGameButton = new ButtonWidget(400, 310, new ThreeImageButtonView(startButtonSequence.GetImageByIndex(0), startButtonSequence.GetImageByIndex(1), startButtonSequence.GetImageByIndex(2)));
            AddChild(StartGameButton);
            StartGameButton.ButtonClick += new ButtonWidget.ButtonEventHandler(OnStartGameButton);

            ImageSequence creditsButtonSequence = (ImageSequence)DataAssetCache.Instance.GetAsset(typeof(ImageSequence), "MainMenuCreditsButton");
            ButtonWidget creditsGameButton = new ButtonWidget(400, 230, new ThreeImageButtonView(creditsButtonSequence.GetImageByIndex(0), creditsButtonSequence.GetImageByIndex(1), creditsButtonSequence.GetImageByIndex(2)));
            AddChild(creditsGameButton);
            creditsGameButton.ButtonClick += new ButtonWidget.ButtonEventHandler(OnShowCreditsButton);

            ImageSequence exitButtonSequence = (ImageSequence)DataAssetCache.Instance.GetAsset(typeof(ImageSequence), "MainMenuExitButton");
            ButtonWidget exitGameButton = new ButtonWidget(400, 170, new ThreeImageButtonView(exitButtonSequence.GetImageByIndex(0), exitButtonSequence.GetImageByIndex(1), exitButtonSequence.GetImageByIndex(2)));
			AddChild(exitGameButton);
			exitGameButton.ButtonClick += new ButtonWidget.ButtonEventHandler(OnExitGameButton);
		}

        public override void OnDraw(RendererBase rendererToDrawWith)
        {
            ImageSequence menuBackground = (ImageSequence)DataAssetCache.Instance.GetAsset(typeof(ImageSequence), "MainMenuBackground");
            rendererToDrawWith.Render(menuBackground.GetImageByIndex(0), 0, 0);

            ImageSequence planetOnMenu = (ImageSequence)DataAssetCache.Instance.GetAsset(typeof(ImageSequence), "PlanetOnMenu");
            rendererToDrawWith.Render(planetOnMenu.GetImageByRatio(planetRatio.Read()), 620, 360);

            base.OnDraw(rendererToDrawWith);
        }

        private void OnStartGameButton(object sender, MouseEventArgs mouseEvent)
		{
			if(StartGame != null)
			{
				StartGame(this);
			}
		}

        private void OnShowCreditsButton(object sender, MouseEventArgs mouseEent)
        {
            if (ShowCredits != null)
            {
                ShowCredits(this);
            }
        }

        private void OnExitGameButton(object sender, MouseEventArgs mouseEvent)
		{
			if(ExitGame != null)
			{
				ExitGame(this);
			}
		}
	}
}
