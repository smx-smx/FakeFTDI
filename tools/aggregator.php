<?php
/**
 * Copyright (C) 2022 Stefano Moioli <smxdev4@gmail.com>
 * This software is provided 'as-is', without any express or implied warranty. In no event will the authors be held liable for any damages arising from the use of this software.
 * Permission is granted to anyone to use this software for any purpose, including commercial applications, and to alter it and redistribute it freely, subject to the following restrictions:
 *  1. The origin of this software must not be misrepresented; you must not claim that you wrote the original software. If you use this software in a product, an acknowledgment in the product documentation would be appreciated but is not required.
 *  2. Altered source versions must be plainly marked as such, and must not be misrepresented as being the original software.
 *  3. This notice may not be removed or altered from any source distribution.
 */

/**
 * adaptation of https://stackoverflow.com/a/4225813/11782802
 */
function hex_dump($data, $newline="\n") {
	static $from = '';
	static $to = '';
	static $width = 16; # number of bytes per line
	static $pad = '.'; # padding for non-visible characters
	
	if ($from==='') {
		for ($i=0; $i<=0xFF; $i++){
			$from .= chr($i);
			$to .= ($i >= 0x20 && $i <= 0x7E) ? chr($i) : $pad;
		}
	}

	// nibbles and spacing
	$line_length = ($width * 2) + $width;

	$hex = str_split(bin2hex($data), $width*2);
	$chars = str_split(strtr($data, $from, $to), $width);

	$offset = 0;
	foreach ($hex as $i => $line){
		//$s_offset = sprintf('%6X',$offset);
		$s_hex = str_pad(
			implode(' ', str_split($line,2)),
			$line_length,
			' ', STR_PAD_RIGHT);

		$s_ascii = str_pad($chars[$i], $width, ' ', STR_PAD_RIGHT);
		//echo "{$s_offset}: {$s_hex} [{$s_ascii}]{$newline}";
		echo "[{$s_ascii}] {$s_hex}{$newline}";
		$offset += $width;
	}
}

if($argc < 2){
	fwrite(STDERR, "Usage: {$argv[0]} <annotations.txt>\n");
	return 1;
}

$cur_addr = 0;
$cur_buf = '';
$cur_mode = '';

$in = fopen($argv[1], 'r');
while(!feof($in)){
	$line = fgets($in);
	if($line === false) continue;

	// line is locale-encoded apparently (e.g. windows-1252 for i^2c, but we don't care)
	// as we throw away everything before ':'
	$p = array_map('trim', explode(':', $line, 3));
	if(count($p) < 3) continue;

	list($proto, $label, $data) = $p;
	$data = strtolower($data);

	$p = array_map('trim', explode(':', $data, 2));
	if(count($p) < 1) continue;
	$cmd = $p[0];
	switch($cmd){
		case 'start':
			break;
		case 'address read':
			$cur_addr = $p[1];
			$cur_buf = '';
			break;
		case 'address write':
			$cur_addr = $p[1];
			$cur_buf = '';
			break;
		case 'write':
			$cur_mode = 'W';
			break;
		case 'read':
			$cur_mode = 'R';
			break;
		case 'data write':
			$cur_buf .= $p[1];
			break;
		case 'stop':
			printf("[{$cur_mode}] {$cur_addr}: ");
			hex_dump(hex2bin($cur_buf));
			break;
	}

}
fclose($in);