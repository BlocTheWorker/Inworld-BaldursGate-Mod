using BaldursGateInworld.Util;
using BaldursGateInworld.Util.Font;
using GameOverlay.Drawing;
using GameOverlay.Windows;
using SharpDX.DirectWrite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Font = GameOverlay.Drawing.Font;
using FontStretch = SharpDX.DirectWrite.FontStretch;
using FontStyle = SharpDX.DirectWrite.FontStyle;
using FontWeight = SharpDX.DirectWrite.FontWeight;
using Image = GameOverlay.Drawing.Image;
using TextAlignment = SharpDX.DirectWrite.TextAlignment;

namespace BaldursGateInworld.Overlay
{
    enum OverlayColors
    {
        TRANSPARENT,
        BLACK,
        WHITE,
        GOLDEN,
        BG_BLACK
    }
    class UIElement
    {
        public Image Image;
        public bool IsSelected, IsConnected;
        public string Id;
        public float X, Y;
        public float Width, Height;
    }

    internal class OverlayLogic : IDisposable
    {
        private GraphicsWindow _window;

        private readonly Dictionary<OverlayColors, SolidBrush> _brushes;
        private readonly Dictionary<string, Font> _fonts;
        private readonly Dictionary<string, Image> _images;
        private Process? _attachedProcess;
        private bool _customFontLoaded, _imagesLoaded, _isConnected;
        readonly string _processName;
        private string _selectedCharacter = string.Empty;
        private FontCollection _fontCollection;
        private Dictionary<string, Image> _imageDictionary;
        private double mousePosX, mousePosY;
        private List<UIElement> _uiElements;
        private UIElement _buttonElement, _panelElement, _disconnectButtonElement;
        private bool _isPanelOpen;
        private string _text;
        private Graphics _gfx;

        public delegate void GameHookHandler();
        public event GameHookHandler? OnHookedToGame;
        public delegate void CharacterActionHandler(string type, string id);
        public event CharacterActionHandler? OnCharacterAction;
        public delegate void GameProcessHandler();
        public event GameProcessHandler? OnGameProcessChange;

        public OverlayLogic(string game)
        {
            _processName = game;

            if (_processName == null)
            {
                throw new ArgumentException("game name cannot be null");
            }

            _imageDictionary = new Dictionary<string, Image>();
            _brushes = new Dictionary<OverlayColors, SolidBrush>();
            _fonts = new Dictionary<string, Font>();
            _images = new Dictionary<string, Image>();

        }

        private void HandleVisibility(object? sender, OverlayVisibilityEventArgs e) { }

        private void SetupImagesAndElements(Graphics gfx)
        {
            Image panel = GetImage(gfx, "portraitFrame");
            Image panelHover = GetImage(gfx, "portraitFrameHover");
            Image panelSelected = GetImage(gfx, "portraitFrameSelected");
            Image panelConnected = GetImage(gfx, "portraitConnected");
            Image button = GetImage(gfx, "button");
            Image buttonHover = GetImage(gfx, "buttonHover");
            Image buttonDisabled = GetImage(gfx, "buttonDisabled");
            Image disconnectButton = GetImage(gfx, "buttonDisconnect");
            Image disconnectButtonDisabled = GetImage(gfx, "buttonDisconnectDisabled");
            Image disconnectButtonHover = GetImage(gfx, "buttonDisconnectHover");
            Image selectPanel = GetImage(gfx, "selectionPanel");
            Image textBackdrop = GetImage(gfx, "textBackdrop");
            _imageDictionary.Add("frame", panel);
            _imageDictionary.Add("frameHover", panelHover);
            _imageDictionary.Add("frameSelected", panelSelected);
            _imageDictionary.Add("frameConnected", panelConnected);
            _imageDictionary.Add("button", button);
            _imageDictionary.Add("buttonHover", buttonHover);
            _imageDictionary.Add("buttonDisabled", buttonDisabled);
            _imageDictionary.Add("disconnectButton", disconnectButton);
            _imageDictionary.Add("disconnectButtonHover", disconnectButtonHover);
            _imageDictionary.Add("disconnectButtonDisabled", disconnectButtonDisabled);
            _imageDictionary.Add("selectPanel", selectPanel);
            _imageDictionary.Add("textBackdrop", textBackdrop);
            _uiElements = new List<UIElement>();
            var companionList = new string[] { "shadowheart", "astarion", "gale", "karlach", "laezel", "wyll" };
            int i = 0;
            foreach (string companionId in companionList)
            {
                Image img = GetImage(gfx, companionId);
                _imageDictionary.Add(companionId, img);
                UIElement elem = new()
                {
                    Image = img,
                    Id = companionId,
                    Height = img.Height,
                    Width = img.Width,
                };
                _uiElements.Add(elem);
                i++;
            }

            _buttonElement = new()
            {
                Id = "button",
                Image = button,
                Height = button.Height,
                Width = button.Width,
            };

            _disconnectButtonElement = new()
            {
                Id = "disconnectButton",
                Image = button,
                Height = button.Height,
                Width = button.Width,
            };

            _panelElement = new()
            {
                Id = "panel",
                Image = selectPanel,
                Height = selectPanel.Height,
                Width = selectPanel.Width,
            };

            _imagesLoaded = true;
        }

        private void LoadCustomFont(Graphics gfx)
        {
            var factory = gfx.GetFontFactory();
            ResourceFontLoader rfl = new ResourceFontLoader(factory);
            _fontCollection = new FontCollection(factory, rfl, rfl.Key);
            _customFontLoaded = true;
        }

        public void StartOverlay()
        {
            while (_attachedProcess == null)
            {
                _attachedProcess = Process.GetProcessesByName(_processName).FirstOrDefault();
            }
            // Wait!
            Thread.Sleep(5000);
            OnHookedToGame?.Invoke();
            _attachedProcess.EnableRaisingEvents = true;
            _attachedProcess.Exited += CloseThisApp;
            _gfx = new Graphics()
            {
                VSync = true,
                TextAntiAliasing = true,
                MeasureFPS = false,
            };

            _window = new GraphicsWindow(0, 0, 800, 600, _gfx)
            {
                IsTopmost = true,
                IsVisible = true,
            };

            _uiElements = new List<UIElement>();
            _window.DestroyGraphics += _window_DestroyGraphics;
            _window.DrawGraphics += _window_DrawGraphics;
            _window.SetupGraphics += _window_SetupGraphics;
            _window.VisibilityChanged += HandleVisibility;
            _window.Create();
            _window.Show();
        }

        private void CloseThisApp(object? sender, EventArgs e)
        {
            Logger.Instance.Log("Disposing the window.");
            _window.Dispose();
            OnGameProcessChange?.Invoke();
        }

        private void _window_DrawGraphics(object sender, DrawGraphicsEventArgs e)
        {
            var gfx = e.Graphics;
            gfx.ClearScene(_brushes[OverlayColors.TRANSPARENT]);

            var dim = WindowUtil.GetWindowDimensions(_attachedProcess);
            int padding = 20;
            _window.Height = dim.Height - dim.LeftTopY - (padding * 2);
            _window.Width = dim.Width - dim.LeftTopX - (padding * 2);
            _window.X = dim.LeftTopX + padding;
            _window.Y = dim.LeftTopY + padding;
            _window.IsTopmost = true;

            if (_isPanelOpen)
            {
                drawBackdropPanel(gfx, (_window.Width / 2f), (_window.Height / 10f));
                drawTitleText(gfx, _window.Width / 2f, _window.Height / 11f);
                drawCompanionCards(gfx, _window.Width / 2, _window.Height / 7f);
                drawConnectButton(gfx, _window.Width / 2f, _window.Height / 3.5f);
                drawDisconnectButton(gfx, _window.Width / 2f, _window.Height / 3.5f);
            }
            if (!string.IsNullOrEmpty(_text))
                drawTextWithBackdrop(gfx, _fonts["crimsonHCenter"], 24, _brushes[OverlayColors.GOLDEN], 0, (7.5f * ((float)_window.Height) / 10f) - 20, _text);
        }

        public void TriggerPanel()
        {
            ResetSelectStates();
            if (_isPanelOpen)
                Logger.Instance.Log("Closing the selection panel");
            else
                Logger.Instance.Log("Opening the selection panel");
            _isPanelOpen = !_isPanelOpen;
        }

        private void ResetSelectStates()
        {
            for (int i = 0; i < _uiElements.Count; i++)
            {
                var elem = _uiElements[i];
                elem.IsSelected = false;
            }
            _selectedCharacter = string.Empty;
        }

        private void drawTextWithBackdrop(Graphics gfx, Font font, int size, IBrush brush, float x, float y, string text)
        {
            Image img = _imageDictionary["textBackdrop"];
            float screenMid = _window.Width / 2f;
            float textY = y;
            var textSize = gfx.MeasureString(font, text);
            int left = (int)(screenMid + (3 * textSize.X / 2f));
            int right = (int)(screenMid - (3 * textSize.X / 4f));
            int top = (int)(textY - 5);
            int bottom = (int)(textY + (textSize.Y) + 5);
            gfx.DrawImage(img, left, top, right, bottom, 0.5f);
            gfx.DrawText(font, size, brush, x, y, text);
        }

        private void drawTitleText(Graphics gfx, float startX, float startY)
        {
            string titleText = "Select companion you want to connect your tadpole with..";
            var tsize = gfx.MeasureString(_fonts["crimsonHCenter"], 28f, titleText);
            gfx.DrawText(_fonts["crimsonHCenter"], 28, _brushes[OverlayColors.GOLDEN], 0, startY, titleText);
        }

        private void drawBackdropPanel(Graphics gfx, float startX, float startY)
        {
            var image = _imageDictionary["selectPanel"];
            var posX = startX - (image.Width / 2f);
            var posY = startY - (image.Height / 10f);
            _panelElement.X = posX;
            _panelElement.Y = posY;
            gfx.DrawImage(image, _panelElement.X, _panelElement.Y);
        }

        private void drawConnectButton(Graphics gfx, float startX, float startY)
        {
            Image button = _imageDictionary["button"];
            float posX = startX - (button.Width + 10);
            float posY = startY;
            _buttonElement.X = posX;
            _buttonElement.Y = posY;
            if (string.IsNullOrEmpty(_selectedCharacter))
            {
                gfx.DrawImage(_imageDictionary["buttonDisabled"], posX, posY);
            }
            else if (IsMouseIn(posX, posY, button.Width, button.Height))
            {
                gfx.DrawImage(_imageDictionary["buttonHover"], posX, posY);
            }
            else
            {
                gfx.DrawImage(button, posX, posY);
            }
        }

        private void drawDisconnectButton(Graphics gfx, float startX, float startY)
        {
            Image button = _imageDictionary["disconnectButton"];
            float posX = startX + 10;
            float posY = startY;
            _disconnectButtonElement.X = posX;
            _disconnectButtonElement.Y = posY;
            if (!_isConnected)
            {
                gfx.DrawImage(_imageDictionary["disconnectButtonDisabled"], posX, posY);
            }
            else if (IsMouseIn(posX, posY, button.Width, button.Height))
            {
                gfx.DrawImage(_imageDictionary["disconnectButtonHover"], posX, posY);
            }
            else
            {
                gfx.DrawImage(button, posX, posY);
            }
        }

        private void drawCompanionCards(Graphics gfx, float startX, float startY)
        {
            Image frameImg = _imageDictionary["frame"];
            startX = startX - (frameImg.Width * 3);
            int i = 0;
            foreach (var uiElement in _uiElements)
            {
                float posX = startX + (frameImg.Width * i);
                float posY = startY;
                uiElement.X = posX;
                uiElement.Y = posY;
                gfx.DrawImage(uiElement.Image, posX, posY);
                if (uiElement.IsConnected)
                {
                    gfx.DrawImage(_imageDictionary["frameConnected"], posX, posY);
                }
                else if (uiElement.IsSelected)
                {
                    gfx.DrawImage(_imageDictionary["frameSelected"], posX, posY);
                }
                else if (!_isConnected && IsMouseIn(posX, posY, frameImg.Width, frameImg.Height))
                {
                    gfx.DrawImage(_imageDictionary["frameHover"], posX, posY);
                }
                else
                {
                    gfx.DrawImage(frameImg, posX, posY);
                }
                i++;
            }
        }

        private void SetConnected(string id)
        {
            for (int i = 0; i < _uiElements.Count; i++)
            {
                var elem = _uiElements[i];
                if (elem.Id == id)
                {
                    elem.IsConnected = true;
                }
                else
                {
                    elem.IsConnected = false;
                }
            }
        }

        public bool HandleClick()
        {
            if (!_isPanelOpen) return true;
            string clickedElement = string.Empty;
            for (int i = 0; i < _uiElements.Count; i++)
            {
                var elem = _uiElements[i];
                if (!_isConnected && IsMouseIn(elem.X, elem.Y, elem.Width, elem.Height))
                {
                    clickedElement = elem.Id;
                    elem.IsSelected = true;
                    _selectedCharacter = clickedElement;
                }
            }

            // If we clicked something, we need to update the cards. Possibly also call callback to make connection.
            if (clickedElement != string.Empty)
            {
                for (int i = 0; i < _uiElements.Count; i++)
                {
                    var elem = _uiElements[i];
                    if (elem.Id != clickedElement)
                    {
                        elem.IsSelected = false;
                    }
                }
            }
            else
            {
                // Check if it's connect button call connect button callback to connect and do stuff.
                if (IsMouseIn(_buttonElement.X, _buttonElement.Y, _buttonElement.Width, _buttonElement.Height))
                {
                    clickedElement = _buttonElement.Id;
                    if (!string.IsNullOrEmpty(_selectedCharacter))
                    {
                        SetConnected(_selectedCharacter);
                        Task.Run(() => { OnCharacterAction?.Invoke("connect", _selectedCharacter); });
                        // connect if button is enabled

                        _isConnected = true;
                        return false;
                    }
                }
                else if (IsMouseIn(_disconnectButtonElement.X, _disconnectButtonElement.Y, _disconnectButtonElement.Width, _disconnectButtonElement.Height))
                {
                    clickedElement = _disconnectButtonElement.Id;
                    // disconnect
                    if (_isConnected)
                    {
                        SetConnected(string.Empty);
                        _isConnected = false;
                        Task.Run(() => { OnCharacterAction?.Invoke("disconnect", string.Empty); });

                        return false;
                    }
                }
                else
                {
                    if (IsMouseIn(_panelElement.X, _panelElement.Y, _panelElement.Width, _panelElement.Height))
                    {
                        clickedElement = _panelElement.Id;
                    }
                }
            }

            return clickedElement == string.Empty;
        }

        private bool IsMouseIn(double x, double y, float width, float height)
        {
            x += _window.X;
            y += _window.Y;
            // Check if mousePosX and mousePosY is in the given rectangle
            if ((mousePosX >= x) && (mousePosX <= x + width) &&
                (mousePosY >= y) && (mousePosY <= y + height))
            {
                return true;
            }
            else
            {
                return false;
            }
        }


        private Image GetImage(Graphics gfx, string name)
        {
            Uri uri = new Uri("pack://application:,,,/Resource/Images/" + name + ".png", UriKind.RelativeOrAbsolute);
            BitmapImage bmi = new BitmapImage(uri);
            byte[] data;
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmi));
            using (MemoryStream ms = new MemoryStream())
            {
                encoder.Save(ms);
                data = ms.ToArray();
            }
            return new Image(gfx, data);
        }

        private void drawAligned(Graphics gfx, Font font, int size, IBrush brush, float x, float y, string text)
        {
            var pt = gfx.MeasureString(font, text);
            drawWithOutline(gfx, font, size, brush, x - pt.X / 2, y - pt.Y / 2, text);
        }

        private void drawWithOutline(Graphics gfx, Font font, int size, IBrush brush, float x, float y, string text)
        {
            float outlineSize = 2f;
            for (float i = -1; i <= 1; i++)
            {
                for (float j = -1; j <= 1; j++)
                {
                    gfx.DrawText(font, size, _brushes[OverlayColors.BLACK], x + (i * outlineSize), y + (j * outlineSize), text);
                }
            }
            gfx.DrawText(font, size, brush, x, y, text);
        }

        bool isHooked = false;

        private void _window_SetupGraphics(object sender, SetupGraphicsEventArgs e)
        {
            var gfx = e.Graphics;

            if (!isHooked)
            {
                isHooked = true;
                InputManager.Instance.HookMouse(this);
            }

            if (e.RecreateResources)
            {
                foreach (var pair in _brushes) pair.Value.Dispose();
                foreach (var pair in _images) pair.Value.Dispose();
            }

            _brushes[OverlayColors.TRANSPARENT] = gfx.CreateSolidBrush(0, 0, 0, 0);
            _brushes[OverlayColors.BLACK] = gfx.CreateSolidBrush(0, 0, 0);
            _brushes[OverlayColors.BG_BLACK] = gfx.CreateSolidBrush(0, 0, 0, 100);
            _brushes[OverlayColors.WHITE] = gfx.CreateSolidBrush(255, 255, 255);

            //gfx.CreateSolidBrush(228, 216, 189); 
            _brushes[OverlayColors.GOLDEN] = gfx.CreateSolidBrush(230, 219, 194);

            if (e.RecreateResources) return;

            if (!_customFontLoaded)
            {
                LoadCustomFont(gfx);
            }

            //#if DEBUG
            Thread.Sleep(2000);
            //#endif

            if (!_imagesLoaded)
            {
                SetupImagesAndElements(gfx);
            }


            var crimson = new TextFormat(e.Graphics.GetFontFactory(), "Crimson Text", _fontCollection, FontWeight.Normal, FontStyle.Normal, FontStretch.Medium, 22) { TextAlignment = TextAlignment.Center, ParagraphAlignment = ParagraphAlignment.Center };
            var crimsonHorizontalCenter = new TextFormat(e.Graphics.GetFontFactory(), "Crimson Text", _fontCollection, FontWeight.Normal, FontStyle.Normal, FontStretch.Medium, 22) { TextAlignment = TextAlignment.Center };
            var aldineFormat = new TextFormat(e.Graphics.GetFontFactory(), "Aldine721 BT", _fontCollection, FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 22);
            var aldineFormatCenter = new TextFormat(e.Graphics.GetFontFactory(), "Aldine721 BT", _fontCollection, FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 22) { TextAlignment = TextAlignment.Center, ParagraphAlignment = ParagraphAlignment.Center };

            _fonts["aldine"] = new Font(aldineFormat);
            _fonts["aldineCenter"] = new Font(aldineFormatCenter);
            _fonts["crimson"] = new Font(crimson);
            _fonts["crimsonHCenter"] = new Font(crimsonHorizontalCenter);
            _fonts["arial"] = gfx.CreateFont("Arial", 22);
        }

        private void _window_DestroyGraphics(object sender, DestroyGraphicsEventArgs e)
        {
            foreach (var pair in _brushes) pair.Value.Dispose();
            foreach (var pair in _fonts) pair.Value.Dispose();
            foreach (var pair in _images) pair.Value.Dispose();
        }


        public void SetText(string text)
        {
            _text = text;
        }

        public void SetMousePos(double X, double Y)
        {
            mousePosX = X;
            mousePosY = Y;
        }

        #region IDisposable Support
        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                _window.Dispose();

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
