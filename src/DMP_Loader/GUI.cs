using Mindmagma.Curses;

namespace Loader {
    class GUI {
    }

    class ScrollWindow {
        public int id;
        private IntPtr _window;
        public int x;
        public int y;
        public int width;
        public int height;
        public uint colorNormal;
        public uint colorHot;
        public bool selected = false;
        public string[] elements = { };
        private int _activeElement;
        private int _scroll;
        private String _noSelectMSG = "";
        private String _noElementMSG = "";
        private String _selectedElement = "";

        public ScrollWindow(int pheight, int pwidth, int py, int px) {
            height = Math.Max(pheight, 2); width = pwidth; y = py; x = px;
        }

        public void Init(int maxheight, string noSelect, string noElement) {
            // _window = NCurses.NewWindow(height, width, y, x);
            _noSelectMSG=noSelect;
            _noElementMSG=noElement;
            _selectedElement = _noSelectMSG;
            height = Math.Max(Math.Min(maxheight,elements.Length+1),2);
            _window = NCurses.NewWindow(height, width, y, x);
        }
        public void Resize(int maxheight) {
            NCurses.DeleteWindow(_window);
            height = Math.Max(Math.Min(maxheight,elements.Length+1),2);
            _window = NCurses.NewWindow(height, width, y, x);
        }

        ~ScrollWindow() {
            NCurses.DeleteWindow(_window);
        }

        public void Prev() {
            if (_activeElement > 0) _activeElement--;
            if (_activeElement < _scroll + 2) _scroll = Math.Max(_activeElement - 1, 0);
        }

        public void Next() {
            if (_activeElement < elements.Length - 1) _activeElement++;
            if (_activeElement > _scroll + (height - 3)) _scroll = Math.Min(elements.Length - (height - 1), _activeElement - (height - 3));
        }

        public void TouchRefresh() {
            NCurses.TouchWindow(_window);
            NCurses.WindowRefresh(_window);
        }

        public string getFullSelected() {
            return _selectedElement;
        }
        
        public void select() {
            _selectedElement=elements[_activeElement];
        }

        public void reselect(string s) {
            _activeElement=0;
            _selectedElement=_noSelectMSG;
            for (int i=0; i< elements.Length;i++) {
                if (elements[i] == s) {
                    _activeElement=i;
                    _selectedElement=s;
                }
            }
        }

        public String getSelected() {
            if (elements.Length > 0) {
                return _selectedElement.Split("/").Last();
            } else {
                return _noElementMSG;
            }
        }

        public void Draw() {
            NCurses.WindowAttributeSet(_window, colorNormal);
            NCurses.MoveWindowAddString(_window, 0, 0, " ".PadRight(width - 1, ' '));

            for (int i = 0; i < Math.Min(elements.Length, height-1); i++) {
                if (i + _scroll < elements.Length) {
                    String elem = elements[i + _scroll].Split("/").Last();
                    if (elem.Length > width - 5) elem = elem.Substring(0, width - 5) + "..";

                    if (i + _scroll == _activeElement) {
                        NCurses.WindowAttributeSet(_window, colorHot);
                        NCurses.MoveWindowAddString(_window, i+1, 0, " " + elem.PadRight(width - 2, ' '));
                    } else {
                        NCurses.WindowAttributeSet(_window, colorNormal);
                        NCurses.MoveWindowAddString(_window, i+1, 0, " " + elem.PadRight(width - 2, ' '));
                    }
                }
            }
        }
    }
}