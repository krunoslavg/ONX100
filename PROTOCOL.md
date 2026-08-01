# ONX-100 Protocol Notes

Reverse-engineered against the provided ONX-100 simulator and compared with the vendor protocol excerpt.

## 1. Transport

| Property | Observed behavior |
|---|---|
| Transport | TCP |
| Port | `4999` |
| Encoding | ASCII |
| Command terminator | `CR` (`\r`, byte `0x0D`) |
| Response terminator | `CRLF` (`\r\n`) |
| Concurrent clients | One active client only |
| Idle timeout | Approximately 60 seconds |
| Idle timeout reset | Any received command resets the timer |

### 1.1 Command framing

Commands must be terminated with `CR`.

```text
PWR ?\r
```

The simulator buffers partial TCP data until it receives `CR`. A command may therefore be split across multiple TCP writes:

```text
"PW"
"R ?"
"\r"
```

The simulator reconstructs this as:

```text
PWR ?
```

Multiple `CR`-terminated commands may also be sent in a single TCP write. They are processed sequentially and responses are returned in the same order.

### 1.2 `LF` and `CRLF`

A standalone `LF` (`\n`) is not a command delimiter. Data remains buffered until a later `CR` arrives.

A command terminated with `CRLF` is processed when the `CR` is received, but the trailing `LF` may remain in the simulator input buffer and contaminate the following command.

For reliable communication, send commands with `CR` only.

## 2. Connection lifecycle

### 2.1 Connection banner

Immediately after every successful TCP connection, the simulator sends:

```text
*HELLO ONX-100 FW:2.13
```

This is an unsolicited connection-level message, not a response to a command.

### 2.2 Second client

The simulator permits only one active TCP client.

If another client connects while a session is active, it receives:

```text
*BUSY
```

The simulator then closes the second connection. The original connection remains active.

### 2.3 Idle disconnect

After approximately 60 seconds without client traffic, the simulator closes the TCP session. Sending any command resets the idle timer.

An orderly idle disconnect was observed to send:

```text
BYE
```

before closing the connection.

### 2.4 Forced simulator shutdown

When the simulator is terminated with `Ctrl+C`, it does not send `BYE` or another protocol message. The client detects a transport-level disconnect, for example:

```text
Unable to read data from the transport connection:
An existing connection was forcibly closed by the remote host.
```

## 3. Command syntax

The parser is strict:

- command names are case-sensitive
- parameters are case-sensitive
- exactly one space is expected between command and parameter
- leading spaces are rejected
- trailing spaces are rejected
- commands are not trimmed or normalized

Examples:

| Input | Result |
|---|---|
| `PWR ?` | Valid |
| `pwr ?` | `ERR 01` |
| `PWR on` | `ERR 02` |
| `PWR?` | `ERR 01` |
| `PWR  ?` | `ERR 02` |
| ` PWR ?` | `ERR 01` |
| `PWR ? ` | `ERR 02` |

## 4. Power

### 4.1 Commands

```text
PWR ON
PWR OFF
PWR ?
```

### 4.2 Query responses

Observed power states:

```text
PWR OFF
PWR WARM
PWR ON
PWR COOL
```

### 4.3 State machine

```text
OFF -> WARM -> ON
ON  -> COOL -> OFF
```

Observed timing:

| Transition | Approximate duration |
|---|---:|
| `PWR ON` to `EVT PWR ON` | 11-12 seconds |
| `PWR OFF` to `EVT PWR OFF` | 7-8 seconds |

A setter returns `OK` immediately, but the actual transition completes only when the corresponding event arrives:

```text
PWR ON
OK
...
EVT PWR ON
```

```text
PWR OFF
OK
...
EVT PWR OFF
```

During the transitions:

- `PWR ?` returns `PWR WARM` or `PWR COOL`
- `IN ?` and input setters return `ERR 03`
- `VOL` commands remain available
- `MUTE` commands remain available

Sending the same power setter while the device is already in that state, or is already transitioning toward that state, returns `OK`. A new power event may not be emitted when no actual state change occurs.

## 5. Input selection

### 5.1 Commands

```text
IN 1
IN 2
IN 3
IN 4
IN ?
```

### 5.2 Behavior

When the device is fully powered on:

- `IN <1-4>` returns `OK`
- `IN ?` returns `IN <1-4>`

Example:

```text
IN 3
OK
IN ?
IN 3
```

Input functions are unavailable while the device is:

- powered off
- warming
- cooling

In those states, input commands return:

```text
ERR 03
```

No dedicated `EVT IN ...` event was observed.

## 6. Volume

### 6.1 Commands

```text
VOL <0-100>
VOL ?
```

### 6.2 Decimal setter, hexadecimal query

The setter accepts a decimal value:

```text
VOL 60
OK
```

The query returns the current value as hexadecimal text:

```text
VOL ?
VOL 3C
```

Examples:

| Decimal volume | Query response |
|---:|---|
| `1` | `VOL 01` |
| `33` | `VOL 21` |
| `40` | `VOL 28` |
| `60` | `VOL 3C` |

The driver must parse the query payload as hexadecimal.

Volume commands work while the device is:

- off
- warming
- on
- cooling

No dedicated volume event was observed.

## 7. Mute

### 7.1 Commands

```text
MUTE ON
MUTE OFF
MUTE ?
```

### 7.2 Behavior

```text
MUTE ON
OK

MUTE ?
MUTE ON
```

```text
MUTE OFF
OK

MUTE ?
MUTE OFF
```

Sending the same state repeatedly still returns `OK`.

Invalid forms such as these return `ERR 02`:

```text
MUTE
MUTE MAYBE
MUTE 1
MUTE on
```

Mute commands work while the device is:

- off
- warming
- on
- cooling

No dedicated `EVT MUTE ...` event was observed.

## 8. Error responses

| Error | Observed meaning |
|---|---|
| `ERR 01` | Unknown command or invalid command shape |
| `ERR 02` | Invalid parameter or invalid formatting |
| `ERR 03` | Command unavailable in the current device state |

Examples:

```text
pwr ?
ERR 01
```

```text
MUTE on
ERR 02
```

```text
PWR OFF
IN ?
ERR 03
```

## 9. Unsolicited events

### 9.1 Power events

```text
EVT PWR ON
EVT PWR OFF
```

These indicate completion of the corresponding power transition.

### 9.2 Signal events

The simulator sends signal events independently of client commands:

```text
EVT SIGNAL 1 OK
EVT SIGNAL 1 LOST
EVT SIGNAL 2 OK
EVT SIGNAL 2 LOST
EVT SIGNAL 3 OK
EVT SIGNAL 3 LOST
EVT SIGNAL 4 OK
EVT SIGNAL 4 LOST
```

Signal events may arrive between a command and its response. The driver must classify them as unsolicited events and must not consume them as command responses.

## 10. Response reliability

The simulator can intentionally drop responses.

Observed examples include:

```text
response dropped: PWR OFF
```

and a dropped `OK` response after a valid setter.

A dropped response does not necessarily close the TCP connection. The device may have processed a setter even when the client received no acknowledgement.

In one test, 1 of 15 valid `PWR ?` responses was dropped. Other runs completed without drops, so the behavior is intermittent.

Implications:

- every command requires a timeout
- a timeout must not automatically be treated as a disconnect
- setter retries must be designed carefully because the first command may already have been applied
- state queries are safer to retry than setters

## 11. Response ordering

When multiple commands are sent together in one TCP write, the simulator processes them sequentially.

Example burst:

```text
VOL 10\r
VOL 20\r
VOL ?\r
MUTE ON\r
MUTE ?\r
IN 2\r
IN ?\r
```

Observed ordered responses while powered on:

```text
OK
OK
VOL 14
OK
MUTE ON
OK
IN 2
```

Unsolicited events may still be inserted between those responses.

## 12. State persistence

### 12.1 TCP reconnect

Device state survives a TCP reconnect.

Confirmed preserved values:

```text
PWR ON
IN 3
VOL 3C
MUTE ON
```

The TCP session and device state are therefore independent.

### 12.2 Simulator restart

A full simulator restart resets the device to:

```text
PWR OFF
IN 1
VOL 28
MUTE OFF
```

`IN 1` can only be queried after the device completes power-on because input commands return `ERR 03` while powered off.

## 13. Driver implementation implications

The driver should:

1. Use one long-running receive loop.
2. Buffer incoming bytes until complete `CRLF`-terminated messages are available.
3. Support fragmented messages and multiple messages in one read.
4. Send commands with `CR` only.
5. Serialize command execution or maintain a strict pending-response queue.
6. Route `EVT ...`, `*HELLO`, `*BUSY`, and `BYE` separately from command responses.
7. Apply a timeout to every command.
8. Keep the connection usable after a command timeout.
9. Model power as `Unknown`, `Off`, `Warming`, `On`, and `Cooling`.
10. Complete power operations on `EVT PWR ON/OFF`, not merely on `OK`.
11. Use longer power-operation timeouts than ordinary command timeouts.
12. Parse volume query values as hexadecimal.
13. Treat `ERR 03` as a state/capability error.
14. Detect idle and forced disconnects and support reconnect.
15. Handle `*BUSY` with backoff instead of a tight reconnect loop.
16. Avoid multiple independent driver instances connecting to the same device.
