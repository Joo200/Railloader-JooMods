# Example of signal patching

The files in this directory are examples of signal patching.

## Patching the signal layout

This is done by the [signals.json](signals.json) file. The file does the following:

The existing panel can be dumped using `/signaldebug dump signals`

1. Change the spans of the existing blocks so they cover the new signal layout correctly.
2. Change the interlocking layout to add a new switch to the layout including routes and outlets.
3. Change the PredicateSignal "br-we" to a new location. Add a new rule so the Fontana branch switch has to be normal.
4. Create the PredicateSignal "br-w-fo" which is the entry signal for fontana branch.
5. Modify the AutoSignals "br-wm" and "br-ws" to double head configuration with the new routes. 

## Patching the panel

The file [ctc-panel.json](ctc-panel.json) is the panel configuration file. It modifies the Mainline panel to add the new switch to the panel.

The existing panel can be dumped using `/signaldebug dump panel`

1. Remove the existing switch and light for br-w at coordinate X=49.
2. Insert the new logic into the panel at the coordinates.

Take note that the "X"-value is a float so to insert new elements a float value can be used.
