using System;
using System.Windows.Media;
using System.Numerics;

public class MandelbrotWindow : ComplexFunctionWindow {

    protected override Color FunctionColor( Complex c ) {
        var z = new Complex( 0.0, 0.0 );
        int iteration = 0;
        int max_iteration = 512;
        while ( iteration < max_iteration ) {
            z = z * z + c;
            if ( z.Magnitude > 2 ) return ColorMap(iteration);
            iteration += 1;
        }
        return Colors.Black;
    }

    private Color ColorMap( int i ) {
        return Color.FromRgb(
            255,
            Convert.ToByte( Math.Abs( 255 - i * 32 % 512 ) ),
            Convert.ToByte( Math.Abs( 255 - i * 8 % 512 ) ) );
    }

} // class
