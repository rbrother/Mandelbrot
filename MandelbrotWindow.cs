using System;
using System.Windows.Media;
using System.Numerics;

public class MandelbrotWindow : ComplexFunctionWindow {

    protected override Color FunctionColor( Complex c ) {
        var z = new Complex( 0.0, 0.0 );
        int count = 0;
        int maxCount = 512;
        while ( count < maxCount ) {
            z = z * z + c;
            if ( z.MagnitudeSqr() > 4 ) return ColorMap(count);
            count += 1;
        }
        return Colors.Black;
    }

} // class
