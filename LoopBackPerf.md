## Copied from support/windows-server/networking/tcpip-performance-known-issues.md


 ## TCP Loopback Performance

    With the Release of  Windows Server 2019, the TCPIP loopback processing model  has  been changed in order to address 
    certain performance bottlenecks  which existed in previous  windows releases.
    The purpose  of this article is   to describe the configuration options   available  to  change  the behavior  of  TCPIP loopback  processing.
    The configuration parameters  are available through the netsh configuration tool.
    
    Each setting can  be set individually for  IPv4  and IPv6.
    The default values  might be different  from Windows Version to   Windows version.
    
    On general purpose  Windows machines, the default values should not be changed.
    If an application developer determines that the loopback data path is  the root cause  for the applications insufficient performance,
    the below information can  be  used  to tailor  the configuration towards the individual  needs  of the application.

   ```console
   netsh int ipv6|ipv4 set gl loopbackexecutionmode=adaptive|inline|worker
   netsh int ipv6|ipv4 set gl loopbackworkercount=<value>
   netsh int ipv6|ipv4 set gl loopbacklargemtu=enable|disable
   ```
 
### Explanation:
 ```console
 Loopbackexecutionmode
   Worker
   ```
    In this mode packets are queued  on the send side  and processed by  a worker thread on the receive side. 
    This mode favors  throughput  over  latency.
    
  ```console
  Inline
  ```
    In this mode  processing is  done  in context  of  application threads both on sender and receiver side. 
    This mode favors latency  over throughput.
    
  ```console
  Adaptive
  ```
    First packets  of the data flow are processing inline,  then packets are deferred to workerthread.
    This mode  tries  to balance latency and throughput. 
    
 ```console
Loopbackworkercount
```
	Allows  to configure the number of  workerthreads  been used 
    
 ```console
Loopbacklargemtu
```
	Allows to configure the use  of large MTU, this should  enabled.

