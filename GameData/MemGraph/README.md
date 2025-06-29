﻿# MemGraph
Copyright (c) 2016 Gerry Iles (Padishar)

This started as a simple plugin that displays a graph of the Mono heap allocation rate and garbage collection, mainly intended 
as a troubleshooting and development aid rather than for general use.  However, I have since devised a way to force Mono to keep 
significantly more free space in the heap, which can significantly reduce the frequency at which the heap fills up and the Mono 
garbage collection causes a stutter, so I have added it to this mod.

## Installation
Copy the MemGraph folder from the zip file into the GameData folder of your KSP installation.

## Usage
	Mod-KeypadMultiply toggles the display of the window.  
	Mod-KeypadPlus increases the vertical scale of the graph.  
	Mod-KeypadMinus decreases the vertical scale of the graph.  
	Mod-KeypadDivide runs a bit of test code controlled by MemGraph/Plugins/PluginData/MemGraph/test.cfg.  
	Mod-End pads the Mono heap with a configurable amount of headroom to reduce frequency of garbage collections.
	Mod-Home writes a marker to the log file (if logging is enabled, see below)
	Mod-PageUp toggles logging

Every second the plugin totals up all the memory allocated on the heap and whether any garbage collections have run.  It also 
displays the current total heap allocation, the maximum heap allocation just before the last garbage collection, the minimum 
just after the last collection, the number of "render" and "physics" updates and the time between the previous two collections.

The graph is 600 pixels wide and shows the last 10 minutes of the memory allocation in green.  If a garbage collection happens 
during the interval, that column of the graph will have a red background.

The test code basically allocates a number of blocks of the specified size and displays which allocations actually cause the 
allocated heap to change.  This allows us to deduce various characteristics of the memory allocator and garbage collection 
mechanisms.

Mod-End activates the heap padding.  The amount of padding is controlled by the MemGraph/Plugins/PluginData/Memgraph/padheap.cfg.  The format 
of the file is very simple.  Each line controls the number of padding blocks allocated of each size.  The first value is the 
size of each block allocated and the second is the number of blocks.  The first values are only present for illustration, 
they don't actually control the size of the blocks, these are hardwired to the sizes in the default configuration.  The 
default configuration allocates around 1024 MB of padding.

I recommend that you run the game normally and load up a situation that has noticeable stutter.  Display the graph, setting 
the scale so the regular allocation rate fits nicely and the garbage collection red lines can be seen.  Let it run for several 
runs of the garbage collector and then hit Mod-End.  After a short pause, the game should continue with a considerably larger 
gap between the collections.  After another few collections hit Mod-End again and it may improve further.  I would appreciate 
feedback about how well this works, e.g. screenshots of the graph taken during this process and after another few collections 
would be very helpful along with details about your setup (and preferably, an output_log.txt/player.log file).  Many thanks to 
Tig for the testing and very well presented data he provided.

One other thing I should add, though it should be obvious with only a little thought, is that the heap padding mechanism is 
only intended for 64 bit versions of the game.  Trying to allocate 1024 MB of extra heap space on the 32 bit version is unlikely 
to be successful and, if it is, then it will probably cause the game to crash before long due to running out of address space. 
It is also unlikely to work effectively if your machine has only 4GB of RAM as the total usage of KSP is likely to grow close 
to 4GB even without loading a save, resulting in virtual memory paging which will seriously hurt performance.

Regarding the padheap.cfg file:
If the default settings aren't working well for you, try the following steps:

1. Increase the﻿ padheap size in your install: /GameData/MemGraph/Plugins/PluginData/MemGraph/padheap.cfg, change the "total" number to 4096 
   or even 6144. See the next section for an example
2. For example, if you have 16 GB:
	a. Look at your task manager to find out ﻿how much free ram you have when the game is running. 
	b. Do this calculation 16 Gb - ~5/6 GB from KSP - 1/2 GB Windows = 8 GB free
	c. This is the amount of RAM you can use for your padheap total size (in MegaByte 4GB=4096MB).  
	d.  Always have some Ram to spare of course. 
Padheap is the volume memgraph is using to save the junk and than deletes it when﻿ its﻿ full.



The code is released under the MIT license (see https://github.com/Gerry1135/MemGraph/blob/master/Graph.cs).

Updated by LinuxGuruGamer

# Logging
The mod now has the ability to write the data to a set of log files.  The files are written in the root of
the game directory.

Logging can be enabled at startup by editing the settings.cfg file.  Set the following to true to turn on logging, false to turn it off:

	enableLogging = true

# Logging Controls
	Mod-Home writes a marker to the log file (if logging is enabled, see below)
	Mod-PageUp toggles logging

The first set of files will only have a single value in them at any time.  This will be the last value which
was plotted.  The name of the file corresponds to the data item as shown on screen:

	dataLog.Cur.txt
	dataLog.HeapMin.txt
	dataLog.Interval.txt
	dataLog.Last.txt
	dataLog.Max.txt
	dataLog.P.txt
	dataLog.R.txt

This last file is a sequential log, and is overwritten every time the game starts.  The log is formatted
as a CSV file, and the first line contains the column headers

	dataLog.Log.txt


