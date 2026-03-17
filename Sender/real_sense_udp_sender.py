import pyrealsense2 as rs
import numpy as np
import socket
import struct
import time

UDP_IP = "0.0.0.0"      # ← Change to your Quest/PC IP
UDP_PORT = 5005
MAX_PACKET_SIZE = 65000 - 8
FRAME_RATE = 15
MAX_DEPTH = 3.0

pipeline = rs.pipeline()
config = rs.config()
config.enable_stream(rs.stream.depth, 640, 480, rs.format.z16, FRAME_RATE)
config.enable_stream(rs.stream.color, 640, 480, rs.format.rgb8, FRAME_RATE)
pipeline.start(config)

align = rs.align(rs.stream.color)

def generate_point_cloud():
    frames = pipeline.wait_for_frames()
    aligned_frames = align.process(frames)
    depth_frame = aligned_frames.get_depth_frame()
    color_frame = aligned_frames.get_color_frame()

    intr = depth_frame.profile.as_video_stream_profile().intrinsics
    depth_data = np.asanyarray(depth_frame.get_data()).astype(float) * 0.001
    color_data = np.asanyarray(color_frame.get_data())

    height, width = depth_data.shape
    v, u = np.meshgrid(np.arange(height), np.arange(width))
    mask = (depth_data[v, u] > 0) & (depth_data[v, u] < MAX_DEPTH)
    u_flat = u[mask].astype(int)
    v_flat = v[mask].astype(int)
    depth_flat = depth_data[v_flat, u_flat]

    points = np.zeros((len(depth_flat), 3), dtype=np.float32)
    for i in range(len(depth_flat)):
        points[i] = rs.rs2_deproject_pixel_to_point(intr, [u_flat[i], v_flat[i]], depth_flat[i])
    colors = color_data[v_flat, u_flat]

    return points, colors

def pack_data(points, colors):
    dtype = np.dtype([('x', '<f4'), ('y', '<f4'), ('z', '<f4'), ('r', 'u1'), ('g', 'u1'), ('b', 'u1')])
    data = np.zeros(len(points), dtype=dtype)
    data['x'] = points[:, 0]
    data['y'] = points[:, 1]
    data['z'] = points[:, 2]
    data['r'] = colors[:, 0]
    data['g'] = colors[:, 1]
    data['b'] = colors[:, 2]
    return data.tobytes()

def stream_data(data):
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    total_chunks = (len(data) + MAX_PACKET_SIZE - 1) // MAX_PACKET_SIZE
    frame_id = int(time.time() * 1000) % (2**32)

    for i in range(total_chunks):
        start = i * MAX_PACKET_SIZE
        end = min(start + MAX_PACKET_SIZE, len(data))
        chunk = data[start:end]
        header = struct.pack('<IHH', frame_id, total_chunks, i)
        packet = header + chunk
        sock.sendto(packet, (UDP_IP, UDP_PORT))

if __name__ == "__main__":
    try:
        print("VRScan Stream - 480p Sender Started")
        while True:
            start_time = time.time()
            points, colors = generate_point_cloud()
            gen_time = time.time() - start_time

            pack_start = time.time()
            data = pack_data(points, colors)
            pack_time = time.time() - pack_start

            stream_start = time.time()
            stream_data(data)
            stream_time = time.time() - stream_start

            elapsed = time.time() - start_time
            sleep_time = max(0, 1.0 / FRAME_RATE - elapsed)

            print(f"Generated {len(points)} pts | Packed {len(data)} B | Stream {stream_time:.3f}s | Total {elapsed:.3f}s")
            time.sleep(sleep_time)
    except KeyboardInterrupt:
        print("Shutting down...")
    finally:
        pipeline.stop()
