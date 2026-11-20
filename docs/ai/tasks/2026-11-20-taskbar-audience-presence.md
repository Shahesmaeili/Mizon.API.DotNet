# SDK audience presence for the Windows preview

The existing socket reads the initial aggregate and accepts new revisions only
while connected. Disconnect, stop and reconnect discard the previous count.
The SDK exposes the aggregate and an authenticated follower-count invocation
over the same socket. Older servers can still connect and leave the aggregate
unavailable until the updated hub is deployed.

The loopback SDK checks cover the typed initial and follower responses,
out-of-order pushes, reconnect and stop. No NuGet publication was performed.
