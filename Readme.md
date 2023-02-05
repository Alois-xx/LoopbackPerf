# LoopbackPerf

A simple Windows loopback test application to check performance over TCP and WebSocket connections. Since Windows 10 1809 and Server 2019 new configuration 
knobs were added to fine tune the performance of the loopback device. This application allows you to fine tune performance and serves as test bed to report potential problems
back to Microsoft if something does not work as expected. 

## Usage


### Start Server
 ``` 
 LoopbackPerf -server
   Start listening http on port 8049
   Press Enter to exit
   TCP: Sent 4000 MB in 0,801 s
   HTTP: Sent 4000 MB in 3,773 s
 ```


 ### Test TCP Socket Performance
 ``` 
 LoopbackPerf -connect tcp -Runs 10
    TCP: Received 4000 MB in 0,83 s 4.810 MB/s
    TCP: Received 4000 MB in 0,79 s 5.053 MB/s
    TCP: Received 4000 MB in 0,87 s 4.622 MB/s
    TCP: Received 4000 MB in 0,77 s 5.166 MB/s
    TCP: Received 4000 MB in 0,82 s 4.871 MB/s
    TCP: Received 4000 MB in 0,81 s 4.959 MB/s
    TCP: Received 4000 MB in 0,77 s 5.172 MB/s
    TCP: Received 4000 MB in 0,85 s 4.714 MB/s
    TCP: Received 4000 MB in 0,84 s 4.741 MB/s
    TCP: Received 4000 MB in 0,85 s 4.693 MB/s
 ```


 ### Test Websocket Performance
 ``` 
 LoopbackPerf -connect http -Runs 10
    HTTP: Received 4000 MB in 2,10 s 1.904 MB/s
    HTTP: Received 4000 MB in 2,10 s 1.900 MB/s
    HTTP: Received 4000 MB in 2,10 s 1.907 MB/s
    HTTP: Received 4000 MB in 2,15 s 1.864 MB/s
    HTTP: Received 4000 MB in 2,10 s 1.907 MB/s
    HTTP: Received 4000 MB in 2,02 s 1.976 MB/s
    HTTP: Received 4000 MB in 2,15 s 1.860 MB/s
    HTTP: Received 4000 MB in 2,09 s 1.915 MB/s
    HTTP: Received 4000 MB in 2,13 s 1.876 MB/s
    HTTP: Received 4000 MB in 2,07 s 1.930 MB/s
 ```

 The most important tuning option are (the tester only uses ipv4. You normally want to set them for ipv4 and ipv6):

 ``` 
 netsh int ipv4 set gl loopbackexecutionmode = inline
 netsh int ipv4 set gl loopbackexecutionmode = worker
 netsh int ipv4 set gl loopbackexecutionmode = adaptive
 ``` 

 You do not restart the server application. Just set the value (it is persistent across reboots!) and run the client tester again. 

 For more information see [LoopBackPerf.md](LoopBackPerf.md).


