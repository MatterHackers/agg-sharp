//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# Port port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007
//
// Permission to copy, use, modify, sell and distribute this software 
// is granted provided this copyright notice appears in all copies. 
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
using System;

using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
    public class CheckBoxViewStates : GuiWidget
    {
        private GuiWidget normal;
        private GuiWidget normalHover;
        private GuiWidget switchNormalToPressed;
        private GuiWidget pressed;
        private GuiWidget pressedHover;
        private GuiWidget switchPressedToNormal;
        private GuiWidget disabled;

        public CheckBoxViewStates(GuiWidget normal, GuiWidget switchNormalToPressed, GuiWidget pressed, GuiWidget switchPressedToNormal)
            : this(normal, normal, switchNormalToPressed, pressed, pressed, switchPressedToNormal, normal)
        {
        }

        public CheckBoxViewStates(GuiWidget normal, GuiWidget normalHover, GuiWidget switchNormalToPressed, 
            GuiWidget pressed, GuiWidget pressedHover, GuiWidget switchPressedToNormal, 
            GuiWidget disabled)
        {
            this.normal = normal;
            this.normalHover = normalHover;
            this.switchNormalToPressed = switchNormalToPressed;

            this.pressed = pressed;
            this.pressedHover = pressedHover;
            this.switchPressedToNormal = switchPressedToNormal;
            this.disabled = disabled;

            AddChild(normal);
            AddChild(normalHover);
            AddChild(switchNormalToPressed);

            AddChild(pressed);
            AddChild(pressedHover);
            AddChild(switchPressedToNormal);
            AddChild(disabled);

            SetBoundsToEncloseChildren();

            normalHover.Visible = false;
            switchNormalToPressed.Visible = false;
            pressed.Visible = false;
            pressedHover.Visible = false;
            switchPressedToNormal.Visible = false;
            disabled.Visible = false;

            normal.Visible = true;
        }

        CheckBox widgetWithHooksToUs;
        void RemoveLinks()
        {
            if (widgetWithHooksToUs != null)
            {
                widgetWithHooksToUs.MouseEnter -= SetCorrectVisibilityStates;
                widgetWithHooksToUs.MouseDown -= SetCorrectVisibilityStates;
                widgetWithHooksToUs.MouseUp -= SetCorrectVisibilityStates;
                widgetWithHooksToUs.MouseLeave -= SetCorrectVisibilityStates;
                widgetWithHooksToUs.CheckedStateChanged -= SetCorrectVisibilityStates;
            }
        }
        
        void CreateLinks(CheckBox parent)
        {
            RemoveLinks();
            
            widgetWithHooksToUs = parent;

            widgetWithHooksToUs.MouseEnter += SetCorrectVisibilityStates;
            widgetWithHooksToUs.MouseDown += SetCorrectVisibilityStates;
            widgetWithHooksToUs.MouseUp += SetCorrectVisibilityStates;
            widgetWithHooksToUs.MouseLeave += SetCorrectVisibilityStates;
            widgetWithHooksToUs.CheckedStateChanged += SetCorrectVisibilityStates;
        }

        public override void OnParentChanged(EventArgs e)
        {
            CreateLinks((CheckBox)Parent);

            base.OnParentChanged(e);
        }

        public override void OnClosed(EventArgs e)
        {
            RemoveLinks();

            base.OnClosed(e);
        }

        public override double Width
        {
            get
            {
                return base.Width;
            }
            set
            {
                base.Width = value;
                normal.Width = this.Width;
                normalHover.Width = this.Width;
                switchNormalToPressed.Width = this.Width;
                pressed.Width = this.Width;
                pressedHover.Width = this.Width;
                switchPressedToNormal.Width = this.Width;
            }
        }

        public void SetCorrectVisibilityStates(object sender, EventArgs e)
        {
            UiThread.RunOnIdle(PostUpdateSetCorrectVisibilityStates);
        }

        public void PostUpdateSetCorrectVisibilityStates(object state)
        {
            CheckBox checkBox = (CheckBox)Parent;
            if (checkBox == null)
            {
                // this can happen if the check box is closing.
                return;
            }

            // the check
            if (!checkBox.Enabled)
            {
                normal.Visible = false;
                normalHover.Visible = false;
                switchNormalToPressed.Visible = false;
                pressed.Visible = false;
                pressedHover.Visible = false;
                switchPressedToNormal.Visible = false;

                disabled.Visible = true;
            }
            else
            {
                if (checkBox.Checked)
                {
                    if (checkBox.FirstWidgetUnderMouse)
                    {
                        if (checkBox.MouseDownOnButton)
                        {
                            normal.Visible = false;
                            normalHover.Visible = false;
                            switchNormalToPressed.Visible = false;
                            pressed.Visible = false;
                            pressedHover.Visible = false;
                            disabled.Visible = false;

                            switchPressedToNormal.Visible = true;
                        }
                        else
                        {
                            normal.Visible = false;
                            normalHover.Visible = false;
                            switchNormalToPressed.Visible = false;
                            pressed.Visible = false;
                            switchPressedToNormal.Visible = false;
                            disabled.Visible = false;

                            pressedHover.Visible = true;
                        }
                    }
                    else
                    {
                        normal.Visible = false;
                        normalHover.Visible = false;
                        switchNormalToPressed.Visible = false;
                        pressedHover.Visible = false;
                        switchPressedToNormal.Visible = false;
                        disabled.Visible = false;

                        pressed.Visible = true;
                    }
                }
                else
                {
                    if (checkBox.FirstWidgetUnderMouse)
                    {
                        if (checkBox.MouseDownOnButton)
                        {
                            normal.Visible = false;
                            normalHover.Visible = false;
                            pressed.Visible = false;
                            pressedHover.Visible = false;
                            switchPressedToNormal.Visible = false;
                            disabled.Visible = false;

                            switchNormalToPressed.Visible = true;
                        }
                        else
                        {
                            normal.Visible = false;
                            switchNormalToPressed.Visible = false;
                            pressed.Visible = false;
                            pressedHover.Visible = false;
                            switchPressedToNormal.Visible = false;
                            disabled.Visible = false;

                            normalHover.Visible = true;
                        }
                    }
                    else
                    {
                        normalHover.Visible = false;
                        switchNormalToPressed.Visible = false;
                        pressed.Visible = false;
                        pressedHover.Visible = false;
                        switchPressedToNormal.Visible = false;
                        disabled.Visible = false;

                        normal.Visible = true;
                    }
                }
            }
        }
    }
}
