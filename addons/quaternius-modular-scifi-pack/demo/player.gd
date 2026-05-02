extends CharacterBody3D


const TURN_RATE := 120.0

const MOVE_RATE := 5.0

const JUMP_DELAY := 1.0

const CLICK_RANGE := 8.0


var jump_cooldown := 0.0
var mouse_look := false
var head_x := 0.0


# Handle mouse input
func _input(event):
	# Handle button presses
	if event is InputEventMouseButton:
		match event.button_index:
			# Detect left-button click
			MouseButton.LEFT:
				if event.pressed:
					_handle_mouse_click()

			# Detect right-button mouse-look
			MouseButton.RIGHT:
				if event.pressed:
					Input.set_mouse_mode(Input.MOUSE_MODE_CAPTURED)
					mouse_look = true
				else:
					Input.set_mouse_mode(Input.MOUSE_MODE_VISIBLE)
					mouse_look = false

	# Handle mouse movement
	if event is InputEventMouseMotion:
		# Handle mouse-look
		if mouse_look:
			rotate(Vector3.UP, -event.relative.x / 200)
			head_x = clamp(head_x - event.relative.y / 200, -PI/2, PI/2)
			$Camera.transform.basis = Basis.from_euler(Vector3(head_x, 0, 0))


# Apply player motion
func _physics_process(delta):
	# Construct control vector
	var control := Vector3.ZERO
	
	# Handle keyboard forwards/backwards
	if Input.is_key_pressed(Key.W):
		control.z -= MOVE_RATE
	if Input.is_key_pressed(Key.S):
		control.z += MOVE_RATE

	# Handle keyboard turn/strafe
	if Input.is_key_pressed(Key.A):
		if mouse_look:
			control.x -= MOVE_RATE
		else:
			rotate(Vector3.UP, delta * deg_to_rad(TURN_RATE))
	if Input.is_key_pressed(Key.D):
		if mouse_look:
			control.x += MOVE_RATE
		else:
			rotate(Vector3.UP, -delta * deg_to_rad(TURN_RATE))

	# Apply control velocity to players X/Z
	control = global_transform.basis * control
	velocity.x = control.x
	velocity.z = control.z

	# Handle keyboard jump
	if Input.is_key_pressed(Key.Space) and jump_cooldown <= 0.0:
		velocity.y = 4.0
		jump_cooldown = JUMP_DELAY
	else:
		velocity.y -= 9.8 * delta
		jump_cooldown = max(0, jump_cooldown - delta)

	# Move the player
	move_and_slide()


# Handle mouse click
func _handle_mouse_click():
	# Get the click ray
	var mouse_pos := get_viewport().get_mouse_position()
	var ray_start : Vector3 = $Camera.project_ray_origin(mouse_pos)
	var ray_end : Vector3 = ray_start + $Camera.project_ray_normal(mouse_pos) * CLICK_RANGE

	# Collide with objects
	var space_state := get_world_3d().direct_space_state
	var query := PhysicsRayQueryParameters3D.create(ray_start, ray_end)
	var collision := space_state.intersect_ray(query)
	if collision.is_empty():
		return

	# Get the colliding object
	var object := collision["collider"] as Node3D
	_handle_object_click(object)


# Handle object click
func _handle_object_click(object : Node3D):
	# If clicking on a door then open/close it
	if object is QuaterniusDoorBody:
		object.door.opened = !object.door.opened
