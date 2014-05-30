using System;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mandelbrot {

    class Mandelbrot {
        [STAThread]
        static void Main( ) {
            var app = new Application( );
            app.Run( new MandelbrotWindow( ) );
        }
    }

}
