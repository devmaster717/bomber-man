package com.bombarena.bluetooth;

import android.app.Activity;
import android.bluetooth.BluetoothAdapter;
import android.bluetooth.BluetoothDevice;
import android.bluetooth.BluetoothServerSocket;
import android.bluetooth.BluetoothSocket;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.os.Build;
import android.os.ParcelUuid;
import android.os.Parcelable;
import android.util.Base64;

import java.io.DataInputStream;
import java.io.DataOutputStream;
import java.io.IOException;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.ConcurrentLinkedQueue;
import java.util.concurrent.atomic.AtomicInteger;

/**
 * Classic Bluetooth RFCOMM transport for Bomb Arena (ADR 0001). It only moves whole messages; all game logic is
 * in the C# core. Unity calls {@link #poll()} every frame to receive events as tab-separated lines:
 *
 * found  address  name  bonded(0/1)  game(0/1)      a nearby or paired phone
 * game   address                                     SDP confirmed it runs Bomb Arena
 * scanDone
 * connected  id  address  name  incoming(0/1)
 * data   id  base64
 * closed id
 * error  message
 *
 * Connections are insecure RFCOMM, so phones need not be paired. Messages are framed with a 4-byte length.
 */
public final class BluetoothLink {
    /** The game's service ID; guests look for it and the host advertises it. */
    public static final UUID SERVICE = UUID.fromString("6f1c1d1e-7b3a-4b8e-9a52-3c2b0d4ab7e1");
    private static final String SERVICE_NAME = "BombArena";
    private static final int MAX_MESSAGE = 1 << 20;

    private final Activity activity;
    private final BluetoothAdapter adapter;
    private final ConcurrentLinkedQueue<String> events = new ConcurrentLinkedQueue<>();
    private final Map<Integer, Connection> connections = new ConcurrentHashMap<>();
    private final AtomicInteger nextId = new AtomicInteger(1);
    private final List<BluetoothDevice> found = new ArrayList<>();

    private BluetoothServerSocket server;
    private Thread acceptThread;
    private BroadcastReceiver receiver;

    public BluetoothLink(Activity activity) {
        this.activity = activity;
        this.adapter = BluetoothAdapter.getDefaultAdapter();
    }

    public boolean isAvailable() { return adapter != null; }

    public boolean isEnabled() {
        try { return adapter != null && adapter.isEnabled(); } catch (SecurityException e) { return false; }
    }

    /** Shows the system prompt to switch Bluetooth on. */
    public void requestEnable() {
        try { activity.startActivity(new Intent(BluetoothAdapter.ACTION_REQUEST_ENABLE)); }
        catch (Exception e) { events.add("error\t" + e.getMessage()); }
    }

    /** Shows the system prompt to make this phone visible to nearby phones for the given time (max 300 s). */
    public void requestDiscoverable(int seconds) {
        try {
            Intent i = new Intent(BluetoothAdapter.ACTION_REQUEST_DISCOVERABLE);
            i.putExtra(BluetoothAdapter.EXTRA_DISCOVERABLE_DURATION, seconds);
            activity.startActivity(i);
        } catch (Exception e) { events.add("error\t" + e.getMessage()); }
    }

    // ---------------- hosting ----------------

    /** Starts accepting guests on the game's service. Returns false if the server socket could not open. */
    public boolean startHosting() {
        stopHosting();
        try {
            server = adapter.listenUsingInsecureRfcommWithServiceRecord(SERVICE_NAME, SERVICE);
        } catch (IOException | SecurityException e) {
            events.add("error\t" + e.getMessage());
            return false;
        }
        final BluetoothServerSocket s = server;
        acceptThread = new Thread(() -> {
            while (true) {
                try {
                    BluetoothSocket socket = s.accept();
                    open(socket, true);
                } catch (IOException e) {
                    break; // closed by stopHosting
                }
            }
        }, "BombArena-accept");
        acceptThread.start();
        return true;
    }

    public void stopHosting() {
        if (server != null) {
            try { server.close(); } catch (IOException ignored) { }
            server = null;
        }
    }

    // ---------------- joining ----------------

    /** Lists paired phones at once, then scans for nearby ones; each is checked for the game's service ID. */
    public void startScan() {
        stopScan();
        found.clear();
        receiver = new BroadcastReceiver() {
            @Override public void onReceive(Context context, Intent intent) {
                String action = intent.getAction();
                if (BluetoothDevice.ACTION_FOUND.equals(action)) {
                    BluetoothDevice d = intent.getParcelableExtra(BluetoothDevice.EXTRA_DEVICE);
                    if (d != null && !found.contains(d)) {
                        found.add(d);
                        report(d, false);
                    }
                } else if (BluetoothAdapter.ACTION_DISCOVERY_FINISHED.equals(action)) {
                    // Service discovery is unreliable while scanning, so check each phone once the scan ends.
                    for (BluetoothDevice d : new ArrayList<>(found)) {
                        try { d.fetchUuidsWithSdp(); } catch (SecurityException ignored) { }
                    }
                    events.add("scanDone");
                } else if (BluetoothDevice.ACTION_UUID.equals(action)) {
                    BluetoothDevice d = intent.getParcelableExtra(BluetoothDevice.EXTRA_DEVICE);
                    Parcelable[] uuids = intent.getParcelableArrayExtra(BluetoothDevice.EXTRA_UUID);
                    if (d != null && hasService(uuids)) events.add("game\t" + d.getAddress());
                }
            }
        };
        IntentFilter filter = new IntentFilter();
        filter.addAction(BluetoothDevice.ACTION_FOUND);
        filter.addAction(BluetoothAdapter.ACTION_DISCOVERY_FINISHED);
        filter.addAction(BluetoothDevice.ACTION_UUID);
        if (Build.VERSION.SDK_INT >= 33) activity.registerReceiver(receiver, filter, Context.RECEIVER_EXPORTED);
        else activity.registerReceiver(receiver, filter);

        try {
            for (BluetoothDevice d : adapter.getBondedDevices()) {
                found.add(d);
                report(d, true);
            }
            adapter.startDiscovery();
        } catch (SecurityException e) {
            events.add("error\t" + e.getMessage());
        }
    }

    public void stopScan() {
        try { if (adapter != null) adapter.cancelDiscovery(); } catch (SecurityException ignored) { }
        if (receiver != null) {
            try { activity.unregisterReceiver(receiver); } catch (IllegalArgumentException ignored) { }
            receiver = null;
        }
    }

    private void report(BluetoothDevice d, boolean bonded) {
        String name;
        boolean game;
        try {
            name = d.getName();
            game = hasService(d.getUuids());
        } catch (SecurityException e) {
            name = null;
            game = false;
        }
        events.add("found\t" + d.getAddress() + "\t" + clean(name == null ? d.getAddress() : name)
                + "\t" + (bonded ? 1 : 0) + "\t" + (game ? 1 : 0));
    }

    private static boolean hasService(Parcelable[] uuids) {
        if (uuids == null) return false;
        String mine = SERVICE.toString();
        String reversed = reverse(SERVICE).toString(); // some phones report SDP UUIDs byte-reversed
        for (Parcelable p : uuids) {
            String u = ((ParcelUuid) p).getUuid().toString();
            if (u.equalsIgnoreCase(mine) || u.equalsIgnoreCase(reversed)) return true;
        }
        return false;
    }

    private static UUID reverse(UUID u) {
        long msb = u.getMostSignificantBits(), lsb = u.getLeastSignificantBits();
        return new UUID(Long.reverseBytes(lsb), Long.reverseBytes(msb));
    }

    /** Connects to a host by address on a background thread; a "connected" or "error" event follows. */
    public void connect(final String address) {
        stopScan();
        new Thread(() -> {
            try {
                BluetoothDevice d = adapter.getRemoteDevice(address);
                BluetoothSocket socket = d.createInsecureRfcommSocketToServiceRecord(SERVICE);
                socket.connect();
                open(socket, false);
            } catch (IOException | SecurityException | IllegalArgumentException e) {
                events.add("error\tCould not connect: " + e.getMessage());
            }
        }, "BombArena-connect").start();
    }

    // ---------------- connections ----------------

    private void open(BluetoothSocket socket, boolean incoming) throws IOException {
        final int id = nextId.getAndIncrement();
        final Connection c = new Connection(socket);
        connections.put(id, c);
        String name;
        try { name = socket.getRemoteDevice().getName(); } catch (SecurityException e) { name = null; }
        events.add("connected\t" + id + "\t" + socket.getRemoteDevice().getAddress() + "\t"
                + clean(name == null ? "?" : name) + "\t" + (incoming ? 1 : 0));
        Thread reader = new Thread(() -> {
            try {
                while (true) {
                    int length = c.in.readInt();
                    if (length < 0 || length > MAX_MESSAGE) throw new IOException("bad frame");
                    byte[] data = new byte[length];
                    c.in.readFully(data);
                    events.add("data\t" + id + "\t" + Base64.encodeToString(data, Base64.NO_WRAP));
                }
            } catch (IOException e) {
                if (connections.remove(id) != null) {
                    c.closeQuietly();
                    events.add("closed\t" + id);
                }
            }
        }, "BombArena-read-" + id);
        reader.start();
    }

    /** Sends one message; returns false if the connection is gone. */
    public boolean send(int id, String base64) {
        Connection c = connections.get(id);
        if (c == null) return false;
        byte[] data = Base64.decode(base64, Base64.NO_WRAP);
        try {
            synchronized (c.out) {
                c.out.writeInt(data.length);
                c.out.write(data);
                c.out.flush();
            }
            return true;
        } catch (IOException e) {
            if (connections.remove(id) != null) {
                c.closeQuietly();
                events.add("closed\t" + id);
            }
            return false;
        }
    }

    /** Closes gracefully: waits briefly so messages already written (e.g. a rejection) reach the other phone. */
    public void close(final int id) {
        final Connection c = connections.remove(id);
        if (c == null) return;
        new Thread(() -> {
            try { Thread.sleep(500); } catch (InterruptedException ignored) { }
            c.closeQuietly();
            events.add("closed\t" + id);
        }, "BombArena-close").start();
    }

    public void shutdown() {
        stopScan();
        stopHosting();
        for (Integer id : new ArrayList<>(connections.keySet())) {
            Connection c = connections.remove(id);
            if (c != null) c.closeQuietly();
        }
    }

    /** The next event, or null when there is none. */
    public String poll() { return events.poll(); }

    private static String clean(String s) { return s.replace('\t', ' ').replace('\n', ' '); }

    private static final class Connection {
        final BluetoothSocket socket;
        final DataInputStream in;
        final DataOutputStream out;

        Connection(BluetoothSocket socket) throws IOException {
            this.socket = socket;
            this.in = new DataInputStream(socket.getInputStream());
            this.out = new DataOutputStream(socket.getOutputStream());
        }

        void closeQuietly() {
            try { socket.close(); } catch (IOException ignored) { }
        }
    }
}
