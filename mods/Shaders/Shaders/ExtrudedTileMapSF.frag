﻿//!#version 450 // Comments with //! are for tricking the Visual Studio GLSL plugin into doing the right thing
//!#extension GL_KHR_vulkan_glsl: enable

// Resource Sets / Uniforms 
layout(binding = 1) uniform texture2DArray DayFloors;   //!
layout(binding = 2) uniform texture2DArray DayWalls;    //!
layout(binding = 3) uniform texture2DArray NightFloors; //!
layout(binding = 4) uniform texture2DArray NightWalls;  //!
layout(binding = 5) uniform sampler TextureSampler;     //!

#include "CommonResources.glsl"

// TODO: Lighting info

// Vertex & Instance data piped through from vertex shader
layout(location = 0) in vec2 iTexCoords;     // Texture Coordinates
layout(location = 1) in flat uint iTextures; // Textures
layout(location = 2) in flat uint iFlags;    // Flags

layout(location = 0) out vec4 OutputColor;

vec4 getFloor(vec3 coords)
{
	vec4 day = texture(sampler2DArray(DayFloors, TextureSampler), coords); //! vec4 day;
	vec4 night = texture(sampler2DArray(NightFloors, TextureSampler), coords); //! vec4 night;
	return mix(day, night, uPaletteBlend);
}

vec4 getWall(vec3 coords)
{
	vec4 day = texture(sampler2DArray(DayWalls, TextureSampler), coords); //! vec4 day;
	vec4 night = texture(sampler2DArray(NightWalls, TextureSampler), coords); //! vec4 night;
	return mix(day, night, uPaletteBlend);
}


void main()

{
	float floorLayer   = float(iTextures & 0x000000ff);
	float ceilingLayer = float((iTextures & 0x0000ff00) >> 8);
	float wallLayer    = float((iTextures & 0x00ff0000) >> 16);
	float overlayLayer = float((iTextures & 0xff000000) >> 24);

	vec4 color;
	switch (iFlags & TF_TEXTURE_TYPE_MASK)
	{
		case TF_TEXTURE_TYPE_FLOOR:
			color = getFloor(vec3(iTexCoords, floorLayer)); //! {}
			break;
		case TF_TEXTURE_TYPE_CEILING:
			color = getFloor(vec3(iTexCoords, ceilingLayer)); //! {}
			break;
		case TF_TEXTURE_TYPE_WALL:
			color = getWall(vec3(iTexCoords, wallLayer)); //! {}
			break;
	}

#ifdef USE_PALETTE
	float redChannel = color[0];
	float index = 255.0f * redChannel;
	if (index == 0)
		color = vec4(0.0f, 0.0f, 0.0f, 0.0f);
		color = vec4(0.0f, 0.0f, 0.0f, 0.0f);
	else
		color = texture(sampler2D(uPalette, PaletteSampler), vec2(redChannel, 0.0f)); //! {}
#endif
	// else if (color.x != 0) color = vec4(color.xx, 0.5f, 1.0f);

	if (color.w == 0.0f)
		discard;

	if ((iFlags & TF_HIGHLIGHT)  != 0) color = color * 1.2; // Highlight
	if ((iFlags & TF_RED_TINT)   != 0) color = vec4(color.x * 1.5f + 0.3f, color.yzw);         // Red tint
	if ((iFlags & TF_GREEN_TINT) != 0) color = vec4(color.x, color.y * 1.5f + 0.3f, color.zw); // Green tint
	if ((iFlags & TF_BLUE_TINT)  != 0) color = vec4(color.xy, color.z * 1.5f + 0.3f, color.w); // Blue tint
	if ((iFlags & TF_TRANSPARENT) != 0) color = vec4(color.xyz, color.w * 0.5f); // Transparent
	if ((iFlags & TF_NO_TEXTURE) != 0) {
		if ((iFlags & TF_TEXTURE_TYPE_MASK) == TF_TEXTURE_TYPE_FLOOR)
			color = vec4(floorLayer / 255.0f, floorLayer / 255.0f, floorLayer / 255.0f, 1.0f);
		else if ((iFlags & TF_TEXTURE_TYPE_MASK) == TF_TEXTURE_TYPE_CEILING)
			color = vec4(ceilingLayer / 255.0f, ceilingLayer / 255.0f, ceilingLayer / 255.0f, 1.0f);
		else // TF_TEXTURE_TYPE_WALL
			color = vec4(wallLayer / 255.0f, wallLayer / 255.0f, wallLayer / 255.0f, 1.0f);
	}

	float depth = gl_FragCoord.z;

	if ((iFlags & TF_TEXTURE_TYPE_MASK) == TF_TEXTURE_TYPE_FLOOR)
		depth = 1.0f;
 
	if ((uEngineFlags & EF_RENDER_DEPTH) != 0)
		color = DEPTH_COLOR(depth);

	if ((uEngineFlags & EF_SHOW_BOUNDING_BOXES) != 0)
	{
		color = mix(color, (3*color + vec4(1.0f)) * 0.25f,
				max(smoothstep(0.47, 0.5, abs(iTexCoords.x-0.5f)),
					smoothstep(0.47, 0.5, abs(iTexCoords.y-0.5f))));
	}

	OutputColor = color;

	gl_FragDepth = ((uEngineFlags & EF_FLIP_DEPTH_RANGE) != 0) ? 1.0f - depth : depth;
}

