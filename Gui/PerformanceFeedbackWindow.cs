using System.IO;

using MatterHackers.Agg.Font;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
    public class PerformanceFeedbackWindow : SystemWindow
    {
        string timingString;
        StyledTypeFace typeFaceToUse;

        public PerformanceFeedbackWindow(StyledTypeFace typeFaceToUse = null)
            : base(700, 480)
        {
            BackgroundColor = RGBA_Bytes.White;
            ShowAsSystemWindow();

            if (typeFaceToUse != null)
            {
                this.typeFaceToUse = typeFaceToUse;
            }
            else
            {
                this.typeFaceToUse = new StyledTypeFace(LiberationSansFont.Instance, 12);
            }
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            TypeFacePrinter stringPrinter = new TypeFacePrinter(timingString, typeFaceToUse, new Vector2(0, Height - 16));
            stringPrinter.DrawFromHintedCache = true;

            stringPrinter.Render(graphics2D, RGBA_Bytes.Black);
            
            base.OnDraw(graphics2D);
        }

        void SetDisplay(string timingString)
        {
            this.timingString = timingString;
            Invalidate();
        }

        public void ShowResults(double totalTimeTracked)
        {
            string timingString = ExecutionTimer.Instance.GetResults(totalTimeTracked);
            SetDisplay(timingString);
        }
    }
}
