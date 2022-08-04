using Mindmagma.Curses;
using System.Collections.Generic;

namespace sample_fireworks
{
    public class MoreCursesLibraryNames : CursesLibraryNames
    {
        public override bool ReplaceLinuxDefaults => true;
        public override List<string> NamesLinux => new List<string> { "libncurses.so.6" };
    }
}
